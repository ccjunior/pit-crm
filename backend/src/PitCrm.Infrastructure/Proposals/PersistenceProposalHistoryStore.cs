using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PitCrm.Application.Demands;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Demands;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class PersistenceProposalHistoryStore(PitCrmDbContext dbContext, IProposalStore proposalStore) : IProposalHistoryStore
{
    public async Task<ProposalHistoryPageDto?> GetTimelineAsync(Guid organizationId, Guid requestId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var requestExists = await dbContext.CommercialRequests.AsNoTracking().AnyAsync(value =>
            value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (!requestExists) return null;

        var now = DateTimeOffset.UtcNow;
        var latestCycle = await dbContext.ProposalRetentionCycles.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
            .OrderByDescending(value => value.Generation).FirstOrDefaultAsync(cancellationToken);
        if (latestCycle is { State: "Active" } && now >= latestCycle.ArchiveAtUtc)
            return new ProposalHistoryPageDto([], page, pageSize, 0);
        var events = dbContext.ProposalTimelineEvents.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.CommercialRequestId == requestId);
        return await ProjectHistoryPageAsync(events, organizationId, page, pageSize, cancellationToken);
    }

    public async Task<ProposalOperatorHistoryPageDto?> GetOperatorTimelineAsync(Guid organizationId, Guid demandId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var demand = await dbContext.Demands.AsNoTracking().Where(value =>
                value.OrganizationId == organizationId && value.Id == demandId && value.SourceProposalRevisionId != null)
            .Select(value => new { value.OrganizationId, value.SourceProposalRevisionId })
            .SingleOrDefaultAsync(cancellationToken);
        if (demand?.SourceProposalRevisionId is not { } revisionId) return null;
        var requestId = await dbContext.ProposalRevisions.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.Id == revisionId)
            .Select(value => (Guid?)value.CommercialRequestId).SingleOrDefaultAsync(cancellationToken);
        if (requestId is null) return null;

        var latestCycle = await dbContext.ProposalRetentionCycles.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
            .OrderByDescending(value => value.Generation).FirstOrDefaultAsync(cancellationToken);
        if (latestCycle is { State: "Active" } && DateTimeOffset.UtcNow >= latestCycle.ArchiveAtUtc)
            return new ProposalOperatorHistoryPageDto([], page, pageSize, 0);
        var query = dbContext.ProposalTimelineEvents.AsNoTracking().Where(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(value => value.Sequence).ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(value => new ProposalOperatorHistoryItemDto(value.EventType,
                value.OccurredAtKnown ? value.OccurredAtUtc : null, value.OccurredAtKnown,
                value.FromStatus, value.ToStatus)).ToListAsync(cancellationToken);
        return new ProposalOperatorHistoryPageDto(rows, page, pageSize, count);
    }

    public async Task<IReadOnlyList<ProposalReasonDto>> GetReasonsAsync(Guid organizationId,
        string? resultStatus, CancellationToken cancellationToken)
    {
        var query = dbContext.ProposalReasons.AsNoTracking().Where(value =>
            value.OrganizationId == organizationId && value.IsActive);
        if (resultStatus is not null) query = query.Where(value => value.ResultStatus == resultStatus);
        return await query.OrderBy(value => value.ResultStatus).ThenBy(value => value.Label)
            .Select(value => new ProposalReasonDto(value.Id, value.ResultStatus, value.Label, value.IsActive))
            .ToListAsync(cancellationToken);
    }

    public Task<ProposalHistoryWriteResult> RecordActivityAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, Guid idempotencyKey, ProposalActivityInput input, DateTimeOffset now,
        CancellationToken cancellationToken) => RecordEventAsync(organizationId, requestId, actorUserId,
        idempotencyKey, ProposalHistoryEventTypes.ActivityRecorded, input, now, cancellationToken);

    public Task<ProposalHistoryWriteResult> RecordCommentAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, Guid idempotencyKey, ProposalActivityInput input, DateTimeOffset now,
        CancellationToken cancellationToken) => RecordEventAsync(organizationId, requestId, actorUserId,
        idempotencyKey, ProposalHistoryEventTypes.CommentAdded, input, now, cancellationToken);

    public async Task<ProposalHistoryWriteResult> ChangeStatusAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, Guid idempotencyKey, ProposalOutcomeInput input, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken))
            return new ProposalHistoryWriteResult(null, ProposalFailure.NotFound);
        var request = await dbContext.CommercialRequests.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalHistoryWriteResult(null, ProposalFailure.NotFound);
        var dbNow = await GetDatabaseUtcNowAsync(cancellationToken);
        if (!TryValidateKey(idempotencyKey, dbNow, out var keyTime))
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        var fingerprint = Fingerprint(new { Operation = "StageChanged", organizationId, requestId,
            actorUserId, input.Version, input.Status, input.ReasonId });
        var (replay, replayConflict) = await FindReplayAsync(organizationId, "Proposal", requestId, idempotencyKey, fingerprint, cancellationToken);
        if (replayConflict) return new ProposalHistoryWriteResult(null, ProposalFailure.VersionConflict);
        if (replay is not null)
        {
            var payload = DeserializeReplay<StatusReplayPayload>(replay.ResultPayloadJson);
            if (payload is null) return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidState);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalHistoryWriteResult(payload.Event, ProposalFailure.None, true, payload.Proposal);
        }
        if (request.Version != input.Version)
            return new ProposalHistoryWriteResult(null, ProposalFailure.VersionConflict);
        if (input.Status is not (CommercialRequest.Evaluating or CommercialRequest.Negotiating
            or CommercialRequest.NotQuoted or CommercialRequest.Rejected or CommercialRequest.Expired or CommercialRequest.Cancelled))
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        if (input.Reason is not null)
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        ProposalReason? reason = null;
        if (input.ReasonId is { } reasonId)
        {
            if (!await LockReasonAsync(organizationId, reasonId, cancellationToken))
                return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
            reason = await dbContext.ProposalReasons.SingleOrDefaultAsync(value =>
                value.OrganizationId == organizationId && value.Id == reasonId
                && value.ResultStatus == input.Status && value.IsActive, cancellationToken);
            if (reason is null) return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        }
        else if (input.Status == CommercialRequest.NotQuoted)
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);

        var previousStatus = request.Status;
        if (IsClosedStatus(previousStatus) && IsClosedStatus(input.Status!))
            await RestoreIfEligibleAsync(organizationId, requestId, dbNow, actorUserId, cancellationToken);
        var converted = await dbContext.DemandDrafts.AnyAsync(value => value.OrganizationId == organizationId
            && value.ProposalRequestId == requestId && value.State == DemandDraft.Converted, cancellationToken);
        try
        {
            request.ChangeStatus(input.Status!, reason?.Label, dbNow, converted);
            var sequence = await ReserveSequenceAsync(organizationId, requestId, cancellationToken);
            var timelineEvent = new ProposalTimelineEvent(Guid.NewGuid(), organizationId, requestId, sequence,
                ProposalHistoryEventTypes.StageChanged, ProposalHistoryActorTypes.Human, actorUserId, null,
                dbNow, previousStatus, input.Status, reason?.Id, reason?.Label);
            dbContext.ProposalTimelineEvents.Add(timelineEvent);
            dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, requestId, timelineEvent.Id,
                "StageChanged", "Transições", actorUserId, dbNow,
                JsonSerializer.Serialize(new { previousStatus, newStatus = input.Status, reasonId = reason?.Id })));
            if (reason is not null)
                dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, requestId, timelineEvent.Id,
                    "StageChanged", "Catálogo", actorUserId, dbNow,
                    JsonSerializer.Serialize(new { reasonId = reason.Id, appliedToStatus = input.Status })));
            if (IsClosedStatus(input.Status!))
            {
                var generation = await dbContext.ProposalRetentionCycles.Where(value =>
                    value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
                    .Select(value => (int?)value.Generation).MaxAsync(cancellationToken) ?? 0;
                dbContext.ProposalRetentionCycles.Add(new ProposalRetentionCycle(organizationId, requestId,
                    generation + 1, dbNow, ProposalHistoryRetentionRules.AddCivilMonthsUtc(dbNow, 24), true));
            }
            else if (IsClosedStatus(previousStatus))
            {
                await RestoreIfEligibleAsync(organizationId, requestId, dbNow, actorUserId, cancellationToken);
            }
            dbContext.ProposalIdempotencyRecords.Add(new ProposalIdempotencyRecord(organizationId, "Proposal",
                requestId, idempotencyKey, "StageChanged", fingerprint, timelineEvent.Id, keyTime, dbNow));
            await dbContext.SaveChangesAsync(cancellationToken);
            var eventDto = await LoadEventDtoAsync(organizationId, timelineEvent.Id, cancellationToken);
            var proposalDto = await proposalStore.FindAsync(organizationId, requestId, cancellationToken);
            if (eventDto is null || proposalDto is null)
                return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidState);
            var idempotencyRecord = await dbContext.ProposalIdempotencyRecords.SingleAsync(value =>
                value.OrganizationId == organizationId && value.Key == idempotencyKey, cancellationToken);
            idempotencyRecord.StoreResultPayload(JsonSerializer.Serialize(new StatusReplayPayload(proposalDto, eventDto)));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalHistoryWriteResult(eventDto, ProposalFailure.None, Proposal: proposalDto);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.VersionConflict);
        }
        catch (DbUpdateException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.VersionConflict);
        }
        catch (CommercialResponsibleRequiredException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.ResponsibleRequired);
        }
        catch (ArgumentException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        }
        catch (InvalidOperationException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidState);
        }
    }

    public async Task<ProposalReasonWriteResult> CreateReasonAsync(Guid organizationId, Guid actorUserId,
        Guid idempotencyKey, ProposalReasonInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (input.ResultStatus is null || input.Label is null)
            return new ProposalReasonWriteResult(null, ProposalFailure.InvalidData);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockOrganizationAsync(organizationId, cancellationToken))
            return new ProposalReasonWriteResult(null, ProposalFailure.NotFound);
        var dbNow = await GetDatabaseUtcNowAsync(cancellationToken);
        var targetId = organizationId;
        if (!TryValidateKey(idempotencyKey, dbNow, out var keyTime))
            return new ProposalReasonWriteResult(null, ProposalFailure.InvalidData);
        var fingerprint = Fingerprint(new { Operation = "ReasonCreated", organizationId,
            actorUserId, input.ResultStatus, label = input.Label.Trim() });
        var (replay, replayConflict) = await FindReplayAsync(organizationId, "ReasonCatalog", targetId, idempotencyKey, fingerprint, cancellationToken);
        if (replayConflict) return new ProposalReasonWriteResult(null, ProposalFailure.VersionConflict);
        if (replay is not null)
        {
            var prior = DeserializeReplay<ProposalReasonDto>(replay.ResultPayloadJson);
            if (prior is null) return new ProposalReasonWriteResult(null, ProposalFailure.InvalidData);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalReasonWriteResult(prior, ProposalFailure.None, true);
        }
        try
        {
            var reason = new ProposalReason(Guid.NewGuid(), organizationId, input.ResultStatus,
                input.Label, actorUserId, dbNow);
            dbContext.ProposalReasons.Add(reason);
            dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, null, null,
                "ReasonCreated", "Catálogo", actorUserId, dbNow,
                JsonSerializer.Serialize(new { reason.Id, reason.ResultStatus, reason.Label })));
            var idempotencyRecord = new ProposalIdempotencyRecord(organizationId,
                "ReasonCatalog", targetId, idempotencyKey, "ReasonCreated", fingerprint, reason.Id, keyTime, dbNow);
            dbContext.ProposalIdempotencyRecords.Add(idempotencyRecord);
            var dto = new ProposalReasonDto(reason.Id, reason.ResultStatus, reason.Label, reason.IsActive);
            await dbContext.SaveChangesAsync(cancellationToken);
            idempotencyRecord.StoreResultPayload(JsonSerializer.Serialize(dto));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalReasonWriteResult(dto, ProposalFailure.None);
        }
        catch (DbUpdateException)
        {
            return new ProposalReasonWriteResult(null, ProposalFailure.VersionConflict);
        }
        catch (ArgumentException)
        {
            return new ProposalReasonWriteResult(null, ProposalFailure.InvalidData);
        }
    }

    public async Task<ProposalReasonWriteResult> DeactivateReasonAsync(Guid organizationId, Guid reasonId,
        Guid actorUserId, Guid idempotencyKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockReasonAsync(organizationId, reasonId, cancellationToken))
            return new ProposalReasonWriteResult(null, ProposalFailure.NotFound);
        if (!await LockOrganizationAsync(organizationId, cancellationToken))
            return new ProposalReasonWriteResult(null, ProposalFailure.NotFound);
        var dbNow = await GetDatabaseUtcNowAsync(cancellationToken);
        if (!TryValidateKey(idempotencyKey, dbNow, out var keyTime))
            return new ProposalReasonWriteResult(null, ProposalFailure.InvalidData);
        var fingerprint = Fingerprint(new { Operation = "ReasonDeactivated", organizationId, reasonId, actorUserId });
        var (replay, replayConflict) = await FindReplayAsync(organizationId, "ProposalReason", reasonId, idempotencyKey, fingerprint, cancellationToken);
        if (replayConflict) return new ProposalReasonWriteResult(null, ProposalFailure.VersionConflict);
        if (replay is not null)
        {
            var prior = DeserializeReplay<ProposalReasonDto>(replay.ResultPayloadJson);
            if (prior is null) return new ProposalReasonWriteResult(null, ProposalFailure.InvalidData);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalReasonWriteResult(prior, ProposalFailure.None, true);
        }
        var reason = await dbContext.ProposalReasons.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Id == reasonId, cancellationToken);
        if (reason is null) return new ProposalReasonWriteResult(null, ProposalFailure.NotFound);
        reason.Deactivate(actorUserId, dbNow);
        dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, null, null,
            "ReasonDeactivated", "Catálogo", actorUserId, dbNow,
            JsonSerializer.Serialize(new { reason.Id, reason.ResultStatus, reason.Label })));
        var idempotencyRecord = new ProposalIdempotencyRecord(organizationId,
            "ProposalReason", reasonId, idempotencyKey, "ReasonDeactivated", fingerprint, reason.Id, keyTime, dbNow);
        dbContext.ProposalIdempotencyRecords.Add(idempotencyRecord);
        try
        {
            var dto = new ProposalReasonDto(reason.Id, reason.ResultStatus, reason.Label, reason.IsActive);
            await dbContext.SaveChangesAsync(cancellationToken);
            idempotencyRecord.StoreResultPayload(JsonSerializer.Serialize(dto));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalReasonWriteResult(dto, ProposalFailure.None);
        }
        catch (DbUpdateException)
        {
            return new ProposalReasonWriteResult(null, ProposalFailure.VersionConflict);
        }
    }

    public async Task<ProposalAuditPageDto?> GetAuditAsync(Guid organizationId, Guid requestId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!await dbContext.CommercialRequests.AsNoTracking().AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == requestId, cancellationToken)) return null;
        var query = dbContext.ProposalAuditEvents.AsNoTracking().Where(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId);
        var count = await query.CountAsync(cancellationToken);
        var rows = await (from audit in query
            join user in dbContext.Users.AsNoTracking()
                on new { audit.OrganizationId, audit.ActorUserId } equals new { user.OrganizationId, ActorUserId = (Guid?)user.Id } into users
            from user in users.DefaultIfEmpty()
            orderby audit.OccurredAtUtc descending, audit.Id
            select new ProposalAuditEventDto(audit.Id, audit.EventType, audit.Category, audit.OccurredAtUtc,
                user == null ? null : user.DisplayName, audit.DetailsJson))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new ProposalAuditPageDto(rows, page, pageSize, count);
    }

    public async Task<ProposalHistoryPageDto?> ReadArchiveAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, int page, int pageSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken)) return null;
        var dbNow = await GetDatabaseUtcNowAsync(cancellationToken);
        var cycle = await dbContext.ProposalRetentionCycles.AsNoTracking().Where(value =>
                value.OrganizationId == organizationId && value.CommercialRequestId == requestId && value.State == "Archived")
            .OrderByDescending(value => value.Generation).FirstOrDefaultAsync(cancellationToken);
        if (cycle is null || cycle.DiscardAtUtc is null || dbNow >= cycle.DiscardAtUtc) return null;
        var archive = await dbContext.ProposalTimelineArchives.AsNoTracking().SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId
            && value.Generation == cycle.Generation, cancellationToken);
        if (archive is null) return null;
        dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, requestId, null,
            "ArchiveRead", "Consultas ao arquivo", actorUserId, dbNow,
            JsonSerializer.Serialize(new { generation = cycle.Generation })));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var archivedEvents = JsonSerializer.Deserialize<List<ArchivedTimelineEvent>>(archive.PayloadJson) ?? [];
        var actorIds = archivedEvents.Where(value => value.ActorUserId.HasValue)
            .Select(value => value.ActorUserId!.Value).Distinct().ToArray();
        var actorNames = await dbContext.Users.AsNoTracking().Where(value => value.OrganizationId == organizationId
                && actorIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.DisplayName, cancellationToken);
        var all = archivedEvents.OrderBy(value => value.Sequence)
            .Select(value => ToDto(value, value.ActorUserId is { } actorId
                ? actorNames.GetValueOrDefault(actorId) : value.SystemCode)).ToArray();
        return new ProposalHistoryPageDto(all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            page, pageSize, all.Length);
    }

    private async Task<ProposalHistoryWriteResult> RecordEventAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, Guid idempotencyKey, string eventType, ProposalActivityInput input,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken))
            return new ProposalHistoryWriteResult(null, ProposalFailure.NotFound);
        var dbNow = await GetDatabaseUtcNowAsync(cancellationToken);
        if (!TryValidateKey(idempotencyKey, dbNow, out var keyTime))
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        var normalizedMentions = (input.MentionedUserIds ?? []).Distinct().Order().ToArray();
        var fingerprint = Fingerprint(new { Operation = eventType, organizationId, requestId, actorUserId,
            type = input.Type?.Trim(), text = input.Text?.Trim(), mentions = normalizedMentions });
        var (replay, replayConflict) = await FindReplayAsync(organizationId, "Proposal", requestId, idempotencyKey, fingerprint, cancellationToken);
        if (replayConflict) return new ProposalHistoryWriteResult(null, ProposalFailure.VersionConflict);
        if (replay is not null)
        {
            var replayEvent = DeserializeReplay<ProposalHistoryEventDto>(replay.ResultPayloadJson);
            if (replayEvent is null) return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidState);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalHistoryWriteResult(replayEvent, ProposalFailure.None, true);
        }
        if (normalizedMentions.Length > 20 || normalizedMentions.Any(value => value == Guid.Empty))
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        if (normalizedMentions.Length > 0 && await dbContext.Users.CountAsync(value =>
                value.OrganizationId == organizationId && value.IsActive && normalizedMentions.Contains(value.Id),
                cancellationToken) != normalizedMentions.Length)
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);

        try
        {
            var sequence = await ReserveSequenceAsync(organizationId, requestId, cancellationToken);
            var text = Required(input.Text, 4000, nameof(input.Text));
            var activityType = eventType == ProposalHistoryEventTypes.ActivityRecorded
                ? Required(input.Type, 60, nameof(input.Type)) : null;
            if (eventType == ProposalHistoryEventTypes.CommentAdded && input.Type is not null)
                return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
            var timelineEvent = new ProposalTimelineEvent(Guid.NewGuid(), organizationId, requestId, sequence,
                eventType, ProposalHistoryActorTypes.Human, actorUserId, null, dbNow,
                activityType: activityType, text: text);
            dbContext.ProposalTimelineEvents.Add(timelineEvent);
            foreach (var userId in normalizedMentions)
                dbContext.ProposalTimelineMentions.Add(new ProposalTimelineMention(Guid.NewGuid(), organizationId,
                    timelineEvent.Id, userId));
            var category = "Atividades e comentários";
            dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, requestId, timelineEvent.Id,
                eventType, category, actorUserId, dbNow,
                JsonSerializer.Serialize(new { eventType, activityType, hasMentions = normalizedMentions.Length > 0 })));
            var idempotencyRecord = new ProposalIdempotencyRecord(organizationId, "Proposal",
                requestId, idempotencyKey, eventType, fingerprint, timelineEvent.Id, keyTime, dbNow);
            dbContext.ProposalIdempotencyRecords.Add(idempotencyRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
            var result = await LoadEventDtoAsync(organizationId, timelineEvent.Id, cancellationToken);
            if (result is null) return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidState);
            idempotencyRecord.StoreResultPayload(JsonSerializer.Serialize(result));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalHistoryWriteResult(result, ProposalFailure.None);
        }
        catch (DbUpdateException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.VersionConflict);
        }
        catch (ArgumentException)
        {
            return new ProposalHistoryWriteResult(null, ProposalFailure.InvalidData);
        }
    }

    private async Task<ProposalHistoryPageDto> ProjectHistoryPageAsync(IQueryable<ProposalTimelineEvent> query,
        Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var count = await query.CountAsync(cancellationToken);
        var events = await query.OrderBy(value => value.Sequence).ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ids = events.Select(value => value.Id).ToArray();
        var mentions = await dbContext.ProposalTimelineMentions.AsNoTracking().Where(value => ids.Contains(value.EventId))
            .OrderBy(value => value.MentionedUserId).Select(value => new { value.EventId, value.MentionedUserId })
            .ToListAsync(cancellationToken);
        var actorIds = events.Where(value => value.ActorUserId.HasValue).Select(value => value.ActorUserId!.Value).Distinct().ToArray();
        var names = await dbContext.Users.AsNoTracking().Where(value => value.OrganizationId == organizationId
                && actorIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.DisplayName, cancellationToken);
        return new ProposalHistoryPageDto(events.Select(value => new ProposalHistoryEventDto(value.Id, value.Sequence,
            value.EventType, value.OccurredAtKnown ? value.OccurredAtUtc : null, value.OccurredAtKnown,
            value.ActorUserId is { } actorId ? names.GetValueOrDefault(actorId) : value.SystemCode,
            value.FromStatus, value.ToStatus, value.ReasonLabelSnapshot, value.ActivityType, value.Text,
            mentions.Where(mention => mention.EventId == value.Id).Select(mention => mention.MentionedUserId).ToArray())).ToArray(),
            page, pageSize, count);
    }

    private async Task<ProposalHistoryEventDto?> LoadEventDtoAsync(Guid organizationId, Guid eventId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.ProposalTimelineEvents.AsNoTracking().Where(value =>
                value.OrganizationId == organizationId && value.Id == eventId)
            .Select(value => new { Event = value, ActorName = value.ActorUserId == null ? value.SystemCode
                : dbContext.Users.Where(user => user.OrganizationId == organizationId && user.Id == value.ActorUserId)
                    .Select(user => user.DisplayName).FirstOrDefault() })
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return null;
        var mentionIds = await dbContext.ProposalTimelineMentions.AsNoTracking().Where(value =>
            value.OrganizationId == organizationId && value.EventId == eventId).OrderBy(value => value.MentionedUserId)
            .Select(value => value.MentionedUserId).ToArrayAsync(cancellationToken);
        var value = item.Event;
        return new ProposalHistoryEventDto(value.Id, value.Sequence, value.EventType,
            value.OccurredAtKnown ? value.OccurredAtUtc : null, value.OccurredAtKnown, item.ActorName,
            value.FromStatus, value.ToStatus, value.ReasonLabelSnapshot, value.ActivityType, value.Text, mentionIds);
    }

    private async Task<(ProposalIdempotencyRecord? Record, bool Conflict)> FindReplayAsync(Guid organizationId,
        string targetType, Guid targetId, Guid key, string fingerprint,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.ProposalIdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Key == key, cancellationToken);
        if (record is null) return (null, false);
        if (record.TargetType != targetType || record.TargetId != targetId) return (record, true);
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(record.Fingerprint),
            Convert.FromHexString(fingerprint)) ? (record, false) : (record, true);
    }

    private static T? DeserializeReplay<T>(string? payloadJson) where T : class =>
        payloadJson is null ? null : JsonSerializer.Deserialize<T>(payloadJson);

    private async Task<long> ReserveSequenceAsync(Guid organizationId, Guid requestId,
        CancellationToken cancellationToken)
    {
        var sequence = await dbContext.ProposalEventSequences.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId, cancellationToken);
        if (sequence is null)
        {
            sequence = new ProposalEventSequence(organizationId, requestId, 0);
            dbContext.ProposalEventSequences.Add(sequence);
        }
        return sequence.ReserveNext();
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

    private async Task<bool> LockOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var result = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "Organizations" WHERE "Id" = {organizationId} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        return result == 1;
    }

    private async Task<bool> LockReasonAsync(Guid organizationId, Guid reasonId, CancellationToken cancellationToken)
    {
        var result = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "ProposalReasons"
            WHERE "OrganizationId" = {organizationId} AND "Id" = {reasonId} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        return result == 1;
    }

    private Task<DateTimeOffset> GetDatabaseUtcNowAsync(CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);

    private static bool TryValidateKey(Guid key, DateTimeOffset dbNow, out DateTimeOffset keyTimestamp)
    {
        keyTimestamp = DateTimeOffset.MinValue;
        if (!ProposalIdempotencyKey.IsVersion7(key)) return false;
        keyTimestamp = ProposalIdempotencyKey.GetTimestampUtc(key);
        return keyTimestamp <= dbNow.AddMinutes(5) && dbNow < keyTimestamp.AddDays(30);
    }

    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    private static ProposalAuditEvent CreateAudit(Guid organizationId, Guid? requestId, Guid? eventId,
        string eventType, string category, Guid actorUserId, DateTimeOffset now, string? details) =>
        new(Guid.NewGuid(), organizationId, eventType, category, ProposalHistoryActorTypes.Human,
            actorUserId, null, now, requestId, eventId, details);

    private sealed record StatusReplayPayload(ProposalDetailsDto Proposal, ProposalHistoryEventDto Event);

    private static bool IsClosedStatus(string status) => status == CommercialRequest.Approved
        || CommercialRequest.ResultStatuses.Contains(status, StringComparer.Ordinal);

    private async Task RestoreIfEligibleAsync(Guid organizationId, Guid requestId, DateTimeOffset now,
        Guid actorUserId, CancellationToken cancellationToken)
    {
        var cycle = await dbContext.ProposalRetentionCycles.Where(value =>
                value.OrganizationId == organizationId && value.CommercialRequestId == requestId
                && (value.State == "Active" || value.State == "Archived"))
            .OrderByDescending(value => value.Generation).FirstOrDefaultAsync(cancellationToken);
        if (cycle is null) return;
        if (cycle.State == "Active")
        {
            cycle.Restore();
            return;
        }
        if (cycle.State != "Archived" || cycle.DiscardAtUtc is null || now >= cycle.DiscardAtUtc) return;
        var archive = await dbContext.ProposalTimelineArchives.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId
            && value.Generation == cycle.Generation, cancellationToken);
        if (archive is null) return;
        foreach (var archivedEvent in JsonSerializer.Deserialize<List<ArchivedTimelineEvent>>(archive.PayloadJson) ?? [])
        {
            dbContext.ProposalTimelineEvents.Add(new ProposalTimelineEvent(archivedEvent.Id, organizationId,
                requestId, archivedEvent.Sequence, archivedEvent.Type, archivedEvent.ActorType,
                archivedEvent.ActorUserId, archivedEvent.SystemCode, archivedEvent.OccurredAtUtc,
                archivedEvent.FromStatus, archivedEvent.ToStatus, archivedEvent.ReasonId,
                archivedEvent.ReasonLabelSnapshot, archivedEvent.ActivityType, archivedEvent.Text,
                archivedEvent.OccurredAtKnown, archivedEvent.SourceLegacyId));
            foreach (var userId in archivedEvent.MentionedUserIds)
                dbContext.ProposalTimelineMentions.Add(new ProposalTimelineMention(Guid.NewGuid(), organizationId,
                    archivedEvent.Id, userId));
        }
        dbContext.ProposalTimelineArchives.Remove(archive);
        cycle.Restore();
        dbContext.ProposalAuditEvents.Add(CreateAudit(organizationId, requestId, null,
            "TimelineRestored", "Consultas ao arquivo", actorUserId, now,
            JsonSerializer.Serialize(new { generation = cycle.Generation })));
    }

    private static string Required(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return value.Trim();
    }

    private sealed record ArchivedTimelineEvent(Guid Id, long Sequence, string Type, string ActorType,
        Guid? ActorUserId, string? SystemCode, DateTimeOffset OccurredAtUtc, bool OccurredAtKnown,
        string? FromStatus, string? ToStatus, Guid? ReasonId, string? ReasonLabelSnapshot,
        string? ActivityType, string? Text, Guid? SourceLegacyId, Guid[] MentionedUserIds);

    private static ProposalHistoryEventDto ToDto(ArchivedTimelineEvent value, string? actorName) => new(value.Id, value.Sequence,
        value.Type, value.OccurredAtKnown ? value.OccurredAtUtc : null, value.OccurredAtKnown,
        actorName, value.FromStatus, value.ToStatus, value.ReasonLabelSnapshot,
        value.ActivityType, value.Text, value.MentionedUserIds);
}
