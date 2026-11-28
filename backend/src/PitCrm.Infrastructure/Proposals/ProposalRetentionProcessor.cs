using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class ProposalRetentionProcessor(
    PitCrmDbContext dbContext,
    IProposalAuditAggregateProcessor auditAggregates) : IProposalRetentionProcessor
{
    private const string SystemCode = "ProposalRetention";

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
        var request = await dbContext.CommercialRequests.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return;
        var latestGeneration = await dbContext.ProposalRetentionCycles.Where(value =>
                value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
            .MaxAsync(value => value.Generation, cancellationToken);
        if (!ProposalHistoryRetentionRules.CanArchive(generation, latestGeneration,
                IsClosedStatus(request.Status))) return;
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
            .Where(value => value.RedactedAtUtc == null).OrderBy(value => value.OccurredAtUtc)
            .Select(value => new { value.Id, value.OrganizationId, value.OccurredAtUtc })
            .Take(500).ToListAsync(cancellationToken);
        var expired = candidates.Where(value =>
            ProposalHistoryRetentionRules.IsExpired(value.OccurredAtUtc, dbNow, 12))
            .OrderBy(value => value.OrganizationId)
            .ThenBy(value => ProposalHistoryRetentionRules.LocalMonthStart(value.OccurredAtUtc))
            .ThenBy(value => value.Id).ToArray();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var redactedProposals = new HashSet<(Guid OrganizationId, Guid RequestId)>();
        foreach (var candidate in expired)
        {
            if (!await TryAcquireAuditLockAsync($"proposal-audit-row:{candidate.Id:N}", cancellationToken)) continue;
            var audit = await dbContext.ProposalAuditEvents.SingleOrDefaultAsync(value =>
                value.OrganizationId == candidate.OrganizationId && value.Id == candidate.Id
                && value.RedactedAtUtc == null, cancellationToken);
            if (audit is null || !ProposalHistoryRetentionRules.IsExpired(audit.OccurredAtUtc, dbNow, 12))
                continue;
            await auditAggregates.ProcessBeforeRedactionAsync(audit, dbNow, cancellationToken);
            if (audit.CommercialRequestId is { } requestId
                && audit.EventType != "AuditDetailsRedacted")
                redactedProposals.Add((audit.OrganizationId, requestId));
            audit.Redact(dbNow);
        }
        foreach (var proposal in redactedProposals)
            dbContext.ProposalAuditEvents.Add(CreateSystemAudit(proposal.OrganizationId, proposal.RequestId,
                "AuditDetailsRedacted", dbNow, null));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditAggregates.SealReadyWindowsAsync(dbNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> TryAcquireAuditLockAsync(string key, CancellationToken cancellationToken) =>
        await dbContext.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({key}, 0)) AS \"Value\"")
            .SingleAsync(cancellationToken);

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

    private static bool IsClosedStatus(string status) => status == CommercialRequest.Approved
        || CommercialRequest.ResultStatuses.Contains(status, StringComparer.Ordinal);

    private sealed record ArchivedTimelineEvent(Guid Id, long Sequence, string Type, string ActorType,
        Guid? ActorUserId, string? SystemCode, DateTimeOffset OccurredAtUtc, bool OccurredAtKnown,
        string? FromStatus, string? ToStatus, Guid? ReasonId, string? ReasonLabelSnapshot,
        string? ActivityType, string? Text, Guid? SourceLegacyId, Guid[] MentionedUserIds);
}
