using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class ProposalRetentionProcessor(PitCrmDbContext dbContext) : IProposalRetentionProcessor
{
    private const string SystemCode = "ProposalRetention";
    private static readonly TimeZoneInfo OrganizationTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        var dbNow = await dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ProposalIdempotencyRecords\" WHERE \"KeyTimestampUtc\" <= {dbNow.AddDays(-30)}", cancellationToken);
        var dueArchive = await dbContext.ProposalRetentionCycles.AsNoTracking()
            .Where(value => value.State == "Active" && value.ArchiveAtUtc <= dbNow)
            .OrderBy(value => value.ArchiveAtUtc).Take(100)
            .Select(value => new { value.OrganizationId, value.CommercialRequestId, value.Generation }).ToListAsync(cancellationToken);
        foreach (var cycle in dueArchive)
            await ArchiveAsync(cycle.OrganizationId, cycle.CommercialRequestId, cycle.Generation, cancellationToken);

        var dueDiscard = await dbContext.ProposalRetentionCycles.AsNoTracking()
            .Where(value => value.State == "Archived" && value.DiscardAtUtc != null && value.DiscardAtUtc <= dbNow)
            .OrderBy(value => value.DiscardAtUtc).Take(100)
            .Select(value => new { value.OrganizationId, value.CommercialRequestId, value.Generation }).ToListAsync(cancellationToken);
        foreach (var cycle in dueDiscard)
            await DiscardAsync(cycle.OrganizationId, cycle.CommercialRequestId, cycle.Generation, cancellationToken);

        await RedactExpiredAuditAsync(dbNow, cancellationToken);
    }

    private async Task ArchiveAsync(Guid organizationId, Guid requestId, int generation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken)) return;
        var now = await GetDatabaseUtcNowAsync(cancellationToken);
        var cycle = await dbContext.ProposalRetentionCycles.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId
            && value.Generation == generation && value.State == "Active" && value.ArchiveAtUtc <= now, cancellationToken);
        if (cycle is null) return;
        var events = await dbContext.ProposalTimelineEvents.Where(value =>
                value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
            .OrderBy(value => value.Sequence).ToListAsync(cancellationToken);
        var eventIds = events.Select(value => value.Id).ToArray();
        var mentions = await dbContext.ProposalTimelineMentions.Where(value =>
            value.OrganizationId == organizationId && eventIds.Contains(value.EventId)).ToListAsync(cancellationToken);
        var mentionMap = mentions.GroupBy(value => value.EventId)
            .ToDictionary(group => group.Key, group => group.Select(value => value.MentionedUserId).ToArray());
        var archived = events.Select(value => new ArchivedTimelineEvent(value.Id, value.Sequence, value.EventType,
            value.ActorType, value.ActorUserId, value.SystemCode, value.OccurredAtUtc, value.OccurredAtKnown,
            value.FromStatus, value.ToStatus, value.ReasonId, value.ReasonLabelSnapshot, value.ActivityType,
            value.Text, value.SourceLegacyId, mentionMap.GetValueOrDefault(value.Id, []))).ToArray();
        var discardAt = ProposalHistoryRetentionRules.AddCivilMonthsUtc(now, 12);
        cycle.Archive(now, discardAt);
        dbContext.ProposalTimelineArchives.Add(new ProposalTimelineArchive(organizationId, requestId,
            generation, JsonSerializer.Serialize(archived), now, discardAt));
        var legacyRows = await dbContext.CommercialActivities.Where(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId).ToListAsync(cancellationToken);
        dbContext.CommercialActivities.RemoveRange(legacyRows);
        dbContext.ProposalTimelineMentions.RemoveRange(mentions);
        dbContext.ProposalTimelineEvents.RemoveRange(events);
        dbContext.ProposalAuditEvents.Add(CreateSystemAudit(organizationId, requestId, "TimelineArchived", now,
            JsonSerializer.Serialize(new { generation, eventCount = events.Count })));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task DiscardAsync(Guid organizationId, Guid requestId, int generation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken)) return;
        var now = await GetDatabaseUtcNowAsync(cancellationToken);
        var cycle = await dbContext.ProposalRetentionCycles.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId
            && value.Generation == generation && value.State == "Archived"
            && value.DiscardAtUtc != null && value.DiscardAtUtc <= now, cancellationToken);
        if (cycle is null) return;
        var archive = await dbContext.ProposalTimelineArchives.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId
            && value.Generation == generation, cancellationToken);
        if (archive is not null) dbContext.ProposalTimelineArchives.Remove(archive);
        cycle.Discard();
        dbContext.ProposalAuditEvents.Add(CreateSystemAudit(organizationId, requestId, "TimelineDiscarded", now,
            JsonSerializer.Serialize(new { generation })));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RedactExpiredAuditAsync(DateTimeOffset dbNow, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.ProposalAuditEvents.AsNoTracking()
            .Where(value => value.DetailsJson != null).OrderBy(value => value.OccurredAtUtc)
            .Select(value => new { value.Id, value.OrganizationId, value.CommercialRequestId,
                value.Category, value.OccurredAtUtc }).ToListAsync(cancellationToken);
        if (candidates.Count == 0) return;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var grouped = candidates.Where(value => value.CommercialRequestId is not null)
            .GroupBy(value =>
            {
                var local = TimeZoneInfo.ConvertTime(value.OccurredAtUtc, OrganizationTimeZone);
                return (value.OrganizationId, Year: local.Year, Month: local.Month, value.Category);
            });
        foreach (var group in grouped)
        {
            var lockKey = $"proposal-audit:{group.Key.OrganizationId:N}:{group.Key.Year}:{group.Key.Month}:{group.Key.Category}";
            if (!await TryAcquireAuditLockAsync(lockKey, cancellationToken)) continue;
            var records = await dbContext.ProposalAuditEvents.Where(value => value.OrganizationId == group.Key.OrganizationId
                && value.Category == group.Key.Category && value.CommercialRequestId != null
                && value.DetailsJson != null && value.OccurredAtUtc >= LocalMonthStartUtc(group.Key.Year, group.Key.Month)
                && value.OccurredAtUtc < LocalMonthStartUtc(group.Key.Year, group.Key.Month).AddMonths(1))
                .ToListAsync(cancellationToken);
            if (records.Count == 0 || dbNow < ProposalHistoryRetentionRules.AddCivilMonthsUtc(
                    records.Max(value => value.OccurredAtUtc), 12)) continue;
            var distinctRequests = records.Select(value => value.CommercialRequestId!.Value).Distinct().LongCount();
            var aggregate = await dbContext.ProposalAuditAggregates.SingleOrDefaultAsync(value =>
                value.OrganizationId == group.Key.OrganizationId && value.Year == group.Key.Year
                && value.Month == group.Key.Month && value.Category == group.Key.Category, cancellationToken);
            if (aggregate is null && distinctRequests >= 10)
                dbContext.ProposalAuditAggregates.Add(new ProposalAuditAggregate(group.Key.OrganizationId,
                    group.Key.Year, group.Key.Month, group.Key.Category, distinctRequests));
            foreach (var audit in records) audit.Redact(dbNow);
        }
        foreach (var candidate in candidates.Where(value => value.CommercialRequestId is null
                     && dbNow >= ProposalHistoryRetentionRules.AddCivilMonthsUtc(value.OccurredAtUtc, 12)))
        {
            if (!await TryAcquireAuditLockAsync($"proposal-audit-row:{candidate.Id:N}", cancellationToken)) continue;
            var audit = await dbContext.ProposalAuditEvents.SingleOrDefaultAsync(value =>
                value.OrganizationId == candidate.OrganizationId && value.Id == candidate.Id
                && value.DetailsJson != null, cancellationToken);
            audit?.Redact(dbNow);
        }
        var redactedOrganizations = dbContext.ChangeTracker.Entries<ProposalAuditEvent>()
            .Where(value => value.State == EntityState.Modified && value.Entity.RedactedAtUtc == dbNow)
            .Select(value => value.Entity.OrganizationId).Distinct().ToArray();
        foreach (var organizationId in redactedOrganizations)
            dbContext.ProposalAuditEvents.Add(CreateSystemAudit(organizationId, null,
                "AuditDetailsRedacted", dbNow, null));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> TryAcquireAuditLockAsync(string key, CancellationToken cancellationToken) =>
        await dbContext.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"")
            .SingleAsync(cancellationToken);

    private static DateTimeOffset LocalMonthStartUtc(int year, int month)
    {
        var local = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, OrganizationTimeZone);
    }

    private async Task<bool> LockRequestAsync(Guid organizationId, Guid requestId,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "CommercialRequests"
            WHERE "OrganizationId" = {organizationId} AND "Id" = {requestId} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        return result == 1;
    }

    private Task<DateTimeOffset> GetDatabaseUtcNowAsync(CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);

    private static ProposalAuditEvent CreateSystemAudit(Guid organizationId, Guid? requestId,
        string type, DateTimeOffset now, string? details) => new(Guid.NewGuid(), organizationId,
        type, "Retenção", ProposalHistoryActorTypes.System, null, SystemCode, now, requestId,
        null, details);

    private sealed record ArchivedTimelineEvent(Guid Id, long Sequence, string Type, string ActorType,
        Guid? ActorUserId, string? SystemCode, DateTimeOffset OccurredAtUtc, bool OccurredAtKnown,
        string? FromStatus, string? ToStatus, Guid? ReasonId, string? ReasonLabelSnapshot,
        string? ActivityType, string? Text, Guid? SourceLegacyId, Guid[] MentionedUserIds);
}
