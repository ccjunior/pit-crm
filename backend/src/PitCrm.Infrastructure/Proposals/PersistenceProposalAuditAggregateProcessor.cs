using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class PersistenceProposalAuditAggregateProcessor(
    PitCrmDbContext dbContext,
    AuditAggregateTokenFactory tokenFactory) : IProposalAuditAggregateProcessor
{
    public async Task ProcessBeforeRedactionAsync(ProposalAuditEvent auditEvent, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Audit aggregation and redaction must share a transaction.");
        if (!ProposalAuditAggregateEventClassifier.TryGetCategory(auditEvent, out var category)
            || auditEvent.CommercialRequestId is not { } requestId) return;

        // AuditDetailsRedacted is created at the instant the original event is redacted;
        // its OccurredAtUtc anchors its own Retention window.
        var monthStart = ProposalHistoryRetentionRules.LocalMonthStart(auditEvent.OccurredAtUtc);
        await AcquireMonthLockAsync(auditEvent.OrganizationId, monthStart, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ProposalAuditAggregateWindows"
                ("OrganizationId", "MonthStart", "Category", "State", "DistinctRequestCount")
            VALUES ({auditEvent.OrganizationId}, {monthStart}, {category},
                {ProposalAuditAggregateWindowStates.Open}, 0)
            ON CONFLICT ("OrganizationId", "MonthStart", "Category") DO NOTHING
            """, cancellationToken);
        var window = await LockWindowAsync(auditEvent.OrganizationId, monthStart, category, cancellationToken);
        var monthInvalidated = dbContext.ChangeTracker.Entries<ProposalAuditAggregateWindow>()
            .Any(entry => entry.Entity.OrganizationId == auditEvent.OrganizationId
                && entry.Entity.MonthStart == monthStart
                && entry.Entity.State == ProposalAuditAggregateWindowStates.Invalidated)
            || await dbContext.ProposalAuditAggregateWindows.AsNoTracking().AnyAsync(value =>
            value.OrganizationId == auditEvent.OrganizationId && value.MonthStart == monthStart
            && value.State == ProposalAuditAggregateWindowStates.Invalidated, cancellationToken);
        if (window.State is ProposalAuditAggregateWindowStates.Sealed or ProposalAuditAggregateWindowStates.Invalidated
            || monthInvalidated)
        {
            var monthWindows = await dbContext.ProposalAuditAggregateWindows.Where(value =>
                value.OrganizationId == auditEvent.OrganizationId && value.MonthStart == monthStart)
                .ToListAsync(cancellationToken);
            foreach (var monthWindow in monthWindows) monthWindow.Invalidate(nowUtc);
            var quarantine = await dbContext.ProposalAuditAggregateQuarantines.SingleOrDefaultAsync(value =>
                value.OrganizationId == auditEvent.OrganizationId && value.MonthStart == monthStart
                && value.Category == category, cancellationToken);
            if (quarantine is null)
                dbContext.ProposalAuditAggregateQuarantines.Add(new ProposalAuditAggregateQuarantine(
                    auditEvent.OrganizationId, monthStart, category, nowUtc));
            else
                quarantine.Add(nowUtc);
            return;
        }

        var token = tokenFactory.Create(auditEvent.OrganizationId, monthStart, category, requestId);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ProposalAuditAggregateContributions"
                ("OrganizationId", "MonthStart", "Category", "RequestToken")
            VALUES ({auditEvent.OrganizationId}, {monthStart}, {category}, {token})
            ON CONFLICT ("OrganizationId", "MonthStart", "Category", "RequestToken") DO NOTHING
            """, cancellationToken);
    }

    public async Task SealReadyWindowsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Audit aggregate sealing must run in a transaction.");
        var now = nowUtc.ToUniversalTime();
        var candidates = await dbContext.ProposalAuditAggregateWindows.AsNoTracking()
            .Where(value => value.State == ProposalAuditAggregateWindowStates.Open)
            .Select(value => new { value.OrganizationId, value.MonthStart }).Distinct()
            .OrderBy(value => value.OrganizationId).ThenBy(value => value.MonthStart).Take(100)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var organizationId = candidate.OrganizationId;
            var monthStart = candidate.MonthStart;
            if (now < ProposalHistoryRetentionRules.AuditAggregateMonthMaturesAtUtc(monthStart)) continue;
            await AcquireMonthLockAsync(organizationId, monthStart, cancellationToken);
            if (await dbContext.ProposalAuditAggregateWindows.AsNoTracking().AnyAsync(value =>
                    value.OrganizationId == organizationId && value.MonthStart == monthStart
                    && value.State == ProposalAuditAggregateWindowStates.Invalidated, cancellationToken)) continue;
            var windows = await dbContext.ProposalAuditAggregateWindows.Where(value =>
                    value.OrganizationId == organizationId && value.MonthStart == monthStart
                    && value.State == ProposalAuditAggregateWindowStates.Open)
                .OrderBy(value => value.Category).ToListAsync(cancellationToken);
            if (windows.Count == 0 || !await IsMigrationReconciledAsync(organizationId, cancellationToken)) continue;

            var range = ProposalHistoryRetentionRules.AuditAggregateMonthUtcRange(monthStart);
            var pending = await dbContext.ProposalAuditEvents.AsNoTracking().Where(value =>
                    value.OrganizationId == organizationId && value.CommercialRequestId != null
                    && value.RedactedAtUtc == null && value.OccurredAtUtc >= range.StartUtc
                    && value.OccurredAtUtc < range.EndUtc)
                .ToListAsync(cancellationToken);
            if (pending.Any(value => ProposalAuditAggregateEventClassifier.TryGetCategory(value, out _))) continue;

            foreach (var window in windows)
            {
                var distinctCount = await dbContext.ProposalAuditAggregateContributions.AsNoTracking()
                    .LongCountAsync(value => value.OrganizationId == organizationId
                        && value.MonthStart == monthStart && value.Category == window.Category,
                        cancellationToken);
                window.Seal(distinctCount, now);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "ProposalAuditAggregateContributions"
                WHERE "OrganizationId" = {organizationId} AND "MonthStart" = {monthStart}
                """, cancellationToken);
        }
    }

    private Task<int> AcquireMonthLockAsync(Guid organizationId, DateOnly monthStart,
        CancellationToken cancellationToken)
    {
        var lockKey = $"proposal-audit-aggregate-month:{organizationId:N}:{monthStart:yyyy-MM}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
    }

    private async Task<ProposalAuditAggregateWindow> LockWindowAsync(Guid organizationId, DateOnly monthStart,
        string category, CancellationToken cancellationToken) => await dbContext.ProposalAuditAggregateWindows
        .FromSqlInterpolated($"""
            SELECT * FROM "ProposalAuditAggregateWindows"
            WHERE "OrganizationId" = {organizationId} AND "MonthStart" = {monthStart}
                AND "Category" = {category}
            FOR UPDATE
            """)
        .SingleAsync(cancellationToken);

    private async Task<bool> IsMigrationReconciledAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var reconciliation = await dbContext.ProposalHistoryMigrationReconciliations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId, cancellationToken);
        if (reconciliation is not null)
            return reconciliation.SourceCount == reconciliation.MigratedCount + reconciliation.QuarantinedCount;

        var hasLegacyActivity = await dbContext.CommercialActivities.AsNoTracking()
            .AnyAsync(value => value.OrganizationId == organizationId, cancellationToken);
        var hasQuarantine = await dbContext.ProposalLegacyQuarantine.AsNoTracking()
            .AnyAsync(value => value.OrganizationId == organizationId, cancellationToken);
        return !hasLegacyActivity && !hasQuarantine;
    }

}

public static class ProposalAuditAggregateEventClassifier
{
    public static bool TryGetCategory(ProposalAuditEvent auditEvent, out string category)
    {
        if (auditEvent.CommercialRequestId is null)
        {
            category = string.Empty;
            return false;
        }
        category = auditEvent.EventType switch
        {
            "StageChanged" when auditEvent.Category == "Transições" => ProposalAuditAggregateCategories.Transitions,
            "StageChanged" when auditEvent.Category == "Catálogo" && HasStructuredReason(auditEvent.DetailsJson)
                => ProposalAuditAggregateCategories.Catalog,
            "ActivityRecorded" or "CommentAdded" => ProposalAuditAggregateCategories.ActivityAndComments,
            "ArchiveRead" => ProposalAuditAggregateCategories.ArchiveReads,
            "TimelineArchived" or "TimelineRestored" or "TimelineDiscarded" or "AuditDetailsRedacted"
                => ProposalAuditAggregateCategories.Retention,
            _ => string.Empty
        };
        return category.Length > 0;
    }

    private static bool HasStructuredReason(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(detailsJson);
            return document.RootElement.TryGetProperty("reasonId", out var reasonId)
                && reasonId.ValueKind == JsonValueKind.String
                && Guid.TryParse(reasonId.GetString(), out var id) && id != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
