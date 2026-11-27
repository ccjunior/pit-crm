using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using PitCrm.Application.Demands;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Demands;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class PersistenceProposalStore(PitCrmDbContext dbContext, IDemandDraftStore demandDraftStore) : IProposalStore
{
    public async Task<ProposalPageDto> SearchAsync(Guid organizationId, string? searchTerm, string? status,
        Guid? customerId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var requests = dbContext.CommercialRequests.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId);
        if (status is not null) requests = requests.Where(value => value.Status == status);
        if (customerId is { } customerFilter) requests = requests.Where(value => value.CustomerId == customerFilter);
        if (from is { } fromDate)
        {
            var fromUtc = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            requests = requests.Where(value => value.UpdatedAtUtc >= fromUtc);
        }
        if (to is { } toDate)
        {
            var untilUtc = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            requests = requests.Where(value => value.UpdatedAtUtc < untilUtc);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = EscapeLike(searchTerm.Trim());
            var pattern = $"%{term}%";
            requests = requests.Where(value => EF.Functions.ILike(value.Description, pattern, "\\")
                || (value.ProspectName != null && EF.Functions.ILike(value.ProspectName, pattern, "\\"))
                || dbContext.CommercialReferences.Any(reference => reference.OrganizationId == organizationId
                    && reference.CommercialRequestId == value.Id && EF.Functions.ILike(reference.Value, pattern, "\\"))
                || dbContext.Customers.Any(customer => customer.OrganizationId == organizationId
                    && customer.Id == value.CustomerId && EF.Functions.ILike(customer.Name, pattern, "\\")));
        }

        var totalCount = await requests.CountAsync(cancellationToken);
        var itemsQuery =
            from request in requests
            join customer in dbContext.Customers.AsNoTracking()
                on new { request.OrganizationId, request.CustomerId } equals new { customer.OrganizationId, CustomerId = (Guid?)customer.Id } into customers
            from customer in customers.DefaultIfEmpty()
            let revision = dbContext.ProposalRevisions.AsNoTracking()
                .Where(item => item.OrganizationId == request.OrganizationId && item.CommercialRequestId == request.Id)
                .OrderByDescending(item => item.Number).FirstOrDefault()
            let responsibleName = dbContext.Users.AsNoTracking()
                .Where(user => user.OrganizationId == request.OrganizationId && user.Id == request.ResponsibleUserId)
                .Select(user => user.DisplayName).FirstOrDefault()
            select new
            {
                Request = request,
                CustomerName = customer == null ? null : customer.Name,
                ResponsibleName = responsibleName,
                RevisionNumber = revision == null ? (int?)null : revision.Number,
                RevisionTotal = revision == null ? (decimal?)null : dbContext.ProposalItems.AsNoTracking()
                    .Where(item => item.OrganizationId == revision.OrganizationId && item.ProposalRevisionId == revision.Id)
                    .Sum(item => (decimal?)item.Amount)
            };
        var rows = await itemsQuery.OrderByDescending(value => value.Request.UpdatedAtUtc)
            .ThenByDescending(value => value.Request.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(value => new ProposalListItemDto(value.Request.Id, value.Request.CustomerId,
                value.CustomerName, value.Request.ProspectName, value.Request.Number, value.Request.ResponsibleUserId,
                value.ResponsibleName, value.Request.Description, value.Request.Origin,
                value.Request.Status, value.Request.Version, value.RevisionNumber, value.RevisionTotal,
                value.Request.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return new ProposalPageDto(rows, page, pageSize, totalCount);
    }

    public async Task<ProposalDetailsDto?> FindAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.CommercialRequests.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.Id == requestId)
            .Select(value => new
            {
                value.Id, value.CustomerId, value.ProspectName, value.ProspectContactName, value.ProspectEmail,
                value.ProspectPhone, value.Description, value.Origin, value.Status, value.Version, value.OutcomeReason,
                value.Number, value.ResponsibleUserId, value.CreatedAtUtc, value.UpdatedAtUtc
            }).SingleOrDefaultAsync(cancellationToken);
        if (request is null) return null;

        var draft = await dbContext.DemandDrafts.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.ProposalRequestId == requestId)
            .Select(value => new { value.State, value.ConvertedDemandId })
            .SingleOrDefaultAsync(cancellationToken);

        var customerName = request.CustomerId is { } customerId
            ? await dbContext.Customers.AsNoTracking().Where(value => value.OrganizationId == organizationId && value.Id == customerId)
                .Select(value => value.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var responsibleName = request.ResponsibleUserId is { } responsibleId
            ? await dbContext.Users.AsNoTracking().Where(value => value.OrganizationId == organizationId && value.Id == responsibleId)
                .Select(value => value.DisplayName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var references = await dbContext.CommercialReferences.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
            .OrderBy(value => value.Type).ThenBy(value => value.Value)
            .Select(value => new CommercialReferenceDto(value.Id, value.Type, value.Value))
            .ToListAsync(cancellationToken);
        var revisionRows = await dbContext.ProposalRevisions.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
            .OrderByDescending(value => value.Number)
            .Select(revision => new
            {
                revision.Id, revision.Number, revision.Status, revision.ValidUntil, revision.Notes,
                revision.CreatedByUserId, revision.CreatedAtUtc, revision.SentAtUtc, revision.SendChannel,
                revision.ApprovedByUserId, revision.ClientApproverName, revision.ApprovalChannel,
                revision.ApprovalNotes, revision.ApprovedAtUtc
            }).ToListAsync(cancellationToken);
        var revisionIds = revisionRows.Select(value => value.Id).ToArray();
        var proposalItems = await dbContext.ProposalItems.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && revisionIds.Contains(value.ProposalRevisionId))
            .OrderBy(value => value.Position)
            .Select(value => new
            {
                value.ProposalRevisionId,
                Item = new ProposalItemDto(value.Id, value.Position, value.Description, value.Amount)
            })
            .ToListAsync(cancellationToken);
        var itemsByRevision = proposalItems.GroupBy(value => value.ProposalRevisionId)
            .ToDictionary(group => group.Key, group => group.Select(value => value.Item).ToArray());
        var revisions = revisionRows.Select(revision =>
        {
            var items = itemsByRevision.GetValueOrDefault(revision.Id) ?? [];
            return new ProposalRevisionDto(revision.Id, revision.Number, revision.Status, revision.ValidUntil,
                revision.Notes, items.Sum(item => item.Amount), items, revision.CreatedByUserId,
                revision.CreatedAtUtc, revision.SentAtUtc, revision.SendChannel, revision.ApprovedByUserId,
                revision.ClientApproverName, revision.ApprovalChannel, revision.ApprovalNotes, revision.ApprovedAtUtc);
        }).ToArray();
        var historyRows = await (
            from historyEvent in dbContext.ProposalTimelineEvents.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on new { historyEvent.OrganizationId, ActorUserId = historyEvent.ActorUserId!.Value }
                equals new { user.OrganizationId, ActorUserId = user.Id }
            where historyEvent.OrganizationId == organizationId && historyEvent.CommercialRequestId == requestId
                && historyEvent.ActorType == ProposalHistoryActorTypes.Human
            orderby historyEvent.Sequence, historyEvent.Id
            select new
            {
                historyEvent.Id,
                ActorUserId = historyEvent.ActorUserId!.Value,
                user.DisplayName,
                EventType = historyEvent.EventType == ProposalHistoryEventTypes.LegacyActivity
                    ? historyEvent.ActivityType! : historyEvent.EventType,
                historyEvent.Text,
                historyEvent.FromStatus,
                historyEvent.ToStatus,
                historyEvent.OccurredAtUtc
            })
            .ToListAsync(cancellationToken);
        var activities = historyRows.Select(value => new ProposalActivityDto(value.Id, value.ActorUserId,
            value.DisplayName, value.EventType,
            value.Text ?? $"Etapa alterada de {value.FromStatus} para {value.ToStatus}.", value.OccurredAtUtc)).ToArray();

        return new ProposalDetailsDto(request.Id, request.CustomerId, customerName, request.ProspectName,
            request.ProspectContactName, request.ProspectEmail, request.ProspectPhone, request.Number,
            request.ResponsibleUserId, responsibleName, references, request.Description,
            request.Origin, request.Status, request.Version, revisions.FirstOrDefault()?.Number,
            revisions, activities, request.CreatedAtUtc, request.UpdatedAtUtc, request.OutcomeReason,
            draft?.State, draft?.ConvertedDemandId);
    }

    public async Task<ProposalCommandResult> CreateAsync(Guid organizationId, Guid actorUserId,
        CreateCommercialRequestInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (input.CustomerId is { } customerId && !await dbContext.Customers.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == customerId, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.CustomerNotFound);
        if (input.ResponsibleUserId is { } responsibleId && !await dbContext.Users.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == responsibleId && value.IsActive, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var year = now.Year;
            var lockKey = $"{organizationId:N}:{year}";
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            var prefix = $"SOL-{year}-";
            var previousNumber = await dbContext.CommercialRequests.AsNoTracking()
                .Where(value => value.OrganizationId == organizationId && value.Number.StartsWith(prefix))
                .OrderByDescending(value => value.Number).Select(value => value.Number).FirstOrDefaultAsync(cancellationToken);
            var nextNumber = previousNumber is null ? 1 : int.Parse(previousNumber[prefix.Length..], System.Globalization.CultureInfo.InvariantCulture) + 1;
            var number = $"{prefix}{nextNumber:D4}";
            var request = new CommercialRequest(Guid.NewGuid(), organizationId, actorUserId, number, input.CustomerId,
                input.ProspectName, input.ProspectContactName, input.ProspectEmail, input.ProspectPhone,
                input.ResponsibleUserId,
                input.Origin!, input.Description!, now);
            request.SetReferences(ToReferenceValues(input.References));
            dbContext.CommercialRequests.Add(request);
            await AddActivityAndTimelineAsync(request, actorUserId, "RequestCreated",
                "Solicitação comercial registrada.", now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalCommandResult(await FindAsync(organizationId, request.Id, cancellationToken), ProposalFailure.None);
        }
        catch (ArgumentException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        }
    }

    public async Task<ProposalCommandResult> UpdateAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        UpdateCommercialRequestInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.NotFound);
        var request = await dbContext.CommercialRequests.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version) return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        if (input.CustomerId is { } customerId && !await dbContext.Customers.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == customerId, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.CustomerNotFound);
        if (input.ResponsibleUserId is { } responsibleId && !await dbContext.Users.AnyAsync(value =>
                value.OrganizationId == organizationId && value.Id == responsibleId && value.IsActive, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        try
        {
            request.UpdateDetails(input.CustomerId, input.ProspectName, input.ProspectContactName,
                input.ProspectEmail, input.ProspectPhone, input.ResponsibleUserId, input.Origin!, input.Description!, now);
            request.SetReferences(ToReferenceValues(input.References));
            await AddActivityAndTimelineAsync(request, actorUserId, "RequestUpdated",
                "Dados da solicitação comercial atualizados.", now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalCommandResult(await FindAsync(organizationId, requestId, cancellationToken), ProposalFailure.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        }
        catch (ArgumentException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        }
        catch (InvalidOperationException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidState);
        }
    }

    public async Task<ProposalCommandResult> CreateRevisionAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, CreateProposalRevisionInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.NotFound);
        var request = await dbContext.CommercialRequests.Include(value => value.Revisions)
                .ThenInclude(value => value.Items)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version) return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        var converted = await dbContext.DemandDrafts.AnyAsync(value => value.OrganizationId == organizationId
            && value.ProposalRequestId == requestId && value.State == "Converted", cancellationToken);
        try
        {
            var previousStatus = request.Status;
            var revision = request.StartRevision(Guid.NewGuid(), actorUserId,
                input.Items!.Select(item => (item.Description!, item.Amount)), input.ValidUntil, input.Notes, now, converted);
            dbContext.ProposalRevisions.Add(revision);
            await AddStageChangeAsync(request, actorUserId, previousStatus, now, cancellationToken);
            if (IsClosedStatus(previousStatus))
                await RestoreHistoryIfEligibleAsync(request.OrganizationId, request.Id, actorUserId, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalCommandResult(await FindAsync(organizationId, requestId, cancellationToken), ProposalFailure.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        }
        catch (ArgumentException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        }
        catch (InvalidOperationException)
        {
            return new ProposalCommandResult(null, converted ? ProposalFailure.DemandAlreadyConverted : ProposalFailure.InvalidState);
        }
    }

    public async Task<ProposalCommandResult> RecordSentAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        RecordProposalSentInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.NotFound);
        var request = await dbContext.CommercialRequests.Include(value => value.Revisions)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version) return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        try
        {
            var previousStatus = request.Status;
            request.RecordSent(input.RevisionId, input.Channel!, input.SentAtUtc, now);
            await AddStageChangeAsync(request, actorUserId, previousStatus, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalCommandResult(await FindAsync(organizationId, requestId, cancellationToken), ProposalFailure.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        }
        catch (ArgumentException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        }
        catch (InvalidOperationException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidState);
        }
    }

    public async Task<ProposalCommandResult> ApproveAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        RecordProposalApprovalInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockRequestAsync(organizationId, requestId, cancellationToken))
            return new ProposalCommandResult(null, ProposalFailure.NotFound);
        var request = await dbContext.CommercialRequests.Include(value => value.Revisions)
                .ThenInclude(value => value.Items)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version)
            return await ResolveApprovalRetryAsync(organizationId, requestId, input.RevisionId, cancellationToken);

        var revision = request.Revisions.SingleOrDefault(value => value.Id == input.RevisionId);
        if (revision is null) return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        try
        {
            var previousStatus = request.Status;
            request.RecordApproval(input.RevisionId, actorUserId, input.ApproverName!, input.Channel!,
                input.Notes, input.ApprovedAtUtc, now);
            await AddStageChangeAsync(request, actorUserId, previousStatus, now, cancellationToken);
            var generation = await dbContext.ProposalRetentionCycles.Where(value =>
                    value.OrganizationId == organizationId && value.CommercialRequestId == requestId)
                .Select(value => (int?)value.Generation).MaxAsync(cancellationToken) ?? 0;
            dbContext.ProposalRetentionCycles.Add(new ProposalRetentionCycle(organizationId, requestId,
                generation + 1, now, ProposalHistoryRetentionRules.AddCivilMonthsUtc(now, 24), true));
            await dbContext.SaveChangesAsync(cancellationToken);

            var draftId = await demandDraftStore.CreateFromApprovedProposalAsync(new ProposalDemandDraftSeed(
                organizationId, request.Id, revision.Id, actorUserId, request.CustomerId, request.ProspectName,
                request.Description, request.Origin,
                revision.Items.OrderBy(item => item.Position).Select(item => new ProposalDemandDraftItemSeed(
                    item.Id, item.Description, item.Amount)).ToArray(), now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProposalCommandResult(await FindAsync(organizationId, requestId, cancellationToken), ProposalFailure.None, draftId);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolveApprovalRetryAsync(organizationId, requestId, input.RevisionId, cancellationToken);
        }
        catch (ArgumentException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        }
        catch (InvalidOperationException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidState);
        }
    }

    private async Task<ProposalCommandResult> ResolveApprovalRetryAsync(Guid organizationId, Guid requestId,
        Guid revisionId, CancellationToken cancellationToken)
    {
        var draft = await dbContext.DemandDrafts.AsNoTracking().SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.ProposalRequestId == requestId
            && value.ProposalRevisionId == revisionId
            && (value.State == DemandDraft.Pending || value.State == DemandDraft.Converted), cancellationToken);
        if (draft is not null)
        {
            var proposal = await FindAsync(organizationId, requestId, cancellationToken);
            if (proposal?.Status == CommercialRequest.Approved)
                return new ProposalCommandResult(proposal, ProposalFailure.None, draft.Id);
        }
        return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
    }

    private async Task AddActivityAndTimelineAsync(CommercialRequest request, Guid actorUserId, string eventType,
        string description, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sequence = await ReserveTimelineSequenceAsync(request.OrganizationId, request.Id, cancellationToken);
        var timelineEvent = new ProposalTimelineEvent(Guid.NewGuid(), request.OrganizationId, request.Id,
            sequence, ProposalHistoryEventTypes.ActivityRecorded, ProposalHistoryActorTypes.Human,
            actorUserId, null, now, activityType: eventType, text: description);
        dbContext.ProposalTimelineEvents.Add(timelineEvent);
        dbContext.ProposalAuditEvents.Add(CreateTimelineAudit(request.OrganizationId, request.Id,
            timelineEvent.Id, "ActivityRecorded", "Atividades e comentários", actorUserId, now,
            JsonSerializer.Serialize(new { activityType = eventType })));
    }

    private async Task AddStageChangeAsync(CommercialRequest request, Guid actorUserId, string previousStatus,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sequence = await ReserveTimelineSequenceAsync(request.OrganizationId, request.Id, cancellationToken);
        var timelineEvent = new ProposalTimelineEvent(Guid.NewGuid(), request.OrganizationId, request.Id,
            sequence, ProposalHistoryEventTypes.StageChanged, ProposalHistoryActorTypes.Human,
            actorUserId, null, now, previousStatus, request.Status);
        dbContext.ProposalTimelineEvents.Add(timelineEvent);
        dbContext.ProposalAuditEvents.Add(CreateTimelineAudit(request.OrganizationId, request.Id,
            timelineEvent.Id, "StageChanged", "Transições", actorUserId, now,
            JsonSerializer.Serialize(new { previousStatus, newStatus = request.Status })));
    }

    private async Task<long> ReserveTimelineSequenceAsync(Guid organizationId, Guid requestId,
        CancellationToken cancellationToken)
    {
        var sequence = await dbContext.ProposalEventSequences.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId, cancellationToken);
        if (sequence is null)
        {
            sequence = new ProposalEventSequence(organizationId, requestId,
                await dbContext.ProposalTimelineEvents.Where(value => value.OrganizationId == organizationId
                        && value.CommercialRequestId == requestId)
                    .Select(value => (long?)value.Sequence).MaxAsync(cancellationToken) ?? 0);
            dbContext.ProposalEventSequences.Add(sequence);
        }
        return sequence.ReserveNext();
    }

    private async Task RestoreHistoryIfEligibleAsync(Guid organizationId, Guid requestId, Guid actorUserId,
        DateTimeOffset now, CancellationToken cancellationToken)
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
        if (cycle.DiscardAtUtc is null || now >= cycle.DiscardAtUtc) return;
        var archive = await dbContext.ProposalTimelineArchives.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.CommercialRequestId == requestId
            && value.Generation == cycle.Generation, cancellationToken);
        if (archive is null) return;
        var archivedEvents = JsonSerializer.Deserialize<List<ArchivedTimelineEvent>>(archive.PayloadJson) ?? [];
        foreach (var archivedEvent in archivedEvents)
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
        dbContext.ProposalAuditEvents.Add(CreateTimelineAudit(organizationId, requestId, null,
            "TimelineRestored", "Consultas ao arquivo", actorUserId, now,
            JsonSerializer.Serialize(new { generation = cycle.Generation })));
    }

    private static bool IsClosedStatus(string status) => status == CommercialRequest.Approved
        || CommercialRequest.ResultStatuses.Contains(status, StringComparer.Ordinal);

    private static ProposalAuditEvent CreateTimelineAudit(Guid organizationId, Guid requestId, Guid? eventId,
        string eventType, string category, Guid actorUserId, DateTimeOffset now, string details) =>
        new(Guid.NewGuid(), organizationId, eventType, category, ProposalHistoryActorTypes.Human,
            actorUserId, null, now, requestId, eventId, details);

    private sealed record ArchivedTimelineEvent(Guid Id, long Sequence, string Type, string ActorType,
        Guid? ActorUserId, string? SystemCode, DateTimeOffset OccurredAtUtc, bool OccurredAtKnown,
        string? FromStatus, string? ToStatus, Guid? ReasonId, string? ReasonLabelSnapshot,
        string? ActivityType, string? Text, Guid? SourceLegacyId, Guid[] MentionedUserIds);

    private async Task<bool> LockRequestAsync(Guid organizationId, Guid requestId,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "CommercialRequests"
            WHERE "OrganizationId" = {organizationId} AND "Id" = {requestId} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        return result == 1;
    }

    private static (string Type, string Value)[] ToReferenceValues(IReadOnlyList<CommercialReferenceInput>? references)
    {
        if (references is null) return [];
        return references.Where(value => !string.IsNullOrWhiteSpace(value.Type) || !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => (value.Type?.Trim() ?? "", value.Value?.Trim() ?? "")).ToArray();
    }

    private static string EscapeLike(string term) => term.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
