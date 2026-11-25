using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using PitCrm.Application.Demands;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Demands;
using PitCrm.Domain.Proposals;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Proposals;

public sealed class PersistenceProposalStore(PitCrmDbContext dbContext, IDemandDraftStore demandDraftStore) : IProposalStore
{
    public async Task<ProposalPageDto> SearchAsync(Guid organizationId, string? searchTerm, string? status,
        Guid? customerId, Guid? responsibleUserId, string? origin, string? overdueFor,
        DateOnly? from, DateOnly? to, string sortBy, bool sortAscending,
        int page, int pageSize, DateOnly today, CancellationToken cancellationToken)
    {
        await using var snapshot = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var requests = dbContext.CommercialRequests.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId);
        if (status is not null) requests = requests.Where(value => value.Status == status);
        if (customerId is { } customerFilter) requests = requests.Where(value => value.CustomerId == customerFilter);
        if (responsibleUserId is { } responsibleFilter) requests = requests.Where(value => value.ResponsibleUserId == responsibleFilter);
        if (origin is not null) requests = requests.Where(value => value.Origin == origin);
        if (overdueFor == "customer") requests = requests.Where(value => value.CustomerDeadline < today);
        else if (overdueFor == "internal") requests = requests.Where(value => value.InternalDeadline < today);
        else if (overdueFor == "either") requests = requests.Where(value =>
            value.CustomerDeadline < today || value.InternalDeadline < today);
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
        var orderedItems = sortBy switch
        {
            "customerDeadline" => sortAscending ? itemsQuery.OrderBy(value => value.Request.CustomerDeadline).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.CustomerDeadline).ThenBy(value => value.Request.Id),
            "internalDeadline" => sortAscending ? itemsQuery.OrderBy(value => value.Request.InternalDeadline).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.InternalDeadline).ThenBy(value => value.Request.Id),
            "nextActionAtUtc" => sortAscending ? itemsQuery.OrderBy(value => value.Request.NextActionAtUtc).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.NextActionAtUtc).ThenBy(value => value.Request.Id),
            "number" => sortAscending ? itemsQuery.OrderBy(value => value.Request.Number).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.Number).ThenBy(value => value.Request.Id),
            _ => sortAscending ? itemsQuery.OrderBy(value => value.Request.UpdatedAtUtc).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.UpdatedAtUtc).ThenByDescending(value => value.Request.Id),
        };
        var rows = await orderedItems.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(value => new ProposalListItemDto(value.Request.Id, value.Request.CustomerId,
                value.CustomerName, value.Request.ProspectName, value.Request.Number, value.Request.ResponsibleUserId,
                value.ResponsibleName, value.Request.Description, value.Request.Origin,
                value.Request.Status, value.Request.Version, value.RevisionNumber, value.RevisionTotal,
                value.Request.UpdatedAtUtc, value.Request.CustomerDeadline, value.Request.InternalDeadline,
                value.Request.ReminderOccurrenceId == null ? null : new ProposalNextActionDto(
                    value.Request.ReminderOccurrenceId.Value, value.Request.ReminderOccurrenceRevision,
                    value.Request.NextActionDescription!, value.Request.NextActionAtUtc!.Value)))
            .ToListAsync(cancellationToken);
        if (snapshot is not null) await snapshot.CommitAsync(cancellationToken);
        return new ProposalPageDto(rows, page, pageSize, totalCount);
    }

    public async Task<ProposalTrackingCommandResult> UpdateTrackingAsync(Guid organizationId, Guid requestId, int version,
        DateOnly? customerDeadline, DateOnly? internalDeadline, string? nextActionDescription,
        DateTimeOffset? nextActionAtUtc, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var request = await dbContext.CommercialRequests.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalTrackingCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != version) return new ProposalTrackingCommandResult(null, ProposalFailure.VersionConflict);
        try
        {
            request.UpdateTracking(customerDeadline, internalDeadline, nextActionDescription, nextActionAtUtc, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            var nextAction = request.ReminderOccurrenceId is null ? null : new ProposalNextActionDto(
                request.ReminderOccurrenceId.Value, request.ReminderOccurrenceRevision,
                request.NextActionDescription!, request.NextActionAtUtc!.Value);
            return new ProposalTrackingCommandResult(new ProposalTrackingDto(request.Id, request.Version,
                request.CustomerDeadline, request.InternalDeadline, request.ReminderOccurrenceRevision, nextAction), ProposalFailure.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ProposalTrackingCommandResult(null, ProposalFailure.VersionConflict);
        }
        catch (ArgumentException)
        {
            return new ProposalTrackingCommandResult(null, ProposalFailure.InvalidData);
        }
    }

    public async Task<MyActionsPageDto> MyActionsAsync(Guid organizationId, Guid userId, int page, int pageSize,
        DateOnly today, CancellationToken cancellationToken)
    {
        var query = dbContext.CommercialRequests.AsNoTracking().Where(value => value.OrganizationId == organizationId
            && value.ResponsibleUserId == userId && value.ReminderOccurrenceId != null);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(value => value.NextActionAtUtc).ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(value => new MyActionItemDto(value.Id, value.Number, value.Status, value.CustomerDeadline,
                value.InternalDeadline, (value.CustomerDeadline != null && value.CustomerDeadline < today)
                    || (value.InternalDeadline != null && value.InternalDeadline < today),
                new ProposalNextActionDto(value.ReminderOccurrenceId!.Value, value.ReminderOccurrenceRevision,
                    value.NextActionDescription!, value.NextActionAtUtc!.Value)))
            .ToListAsync(cancellationToken);
        return new MyActionsPageDto(rows, page, pageSize, count);
    }

    public async Task<ProposalKanbanDto> KanbanAsync(Guid organizationId, string? searchTerm, Guid? customerId,
        Guid? responsibleUserId, string? origin, string? overdueFor, DateOnly? from, DateOnly? to,
        string sortBy, bool sortAscending, int pageSizePerColumn, IReadOnlyDictionary<string, string?> cursors, DateOnly today,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var columns = new List<ProposalKanbanColumnDto>();
        foreach (var status in CommercialRequest.Statuses)
        {
            var page = DecodeCursor(cursors.GetValueOrDefault(status));
            var results = await SearchAsync(organizationId, searchTerm, status, customerId, responsibleUserId,
                origin, overdueFor, from, to, sortBy, sortAscending, page, pageSizePerColumn, today, cancellationToken);
            var next = page * (long)pageSizePerColumn < results.TotalCount
                ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes((page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
                : null;
            columns.Add(new ProposalKanbanColumnDto(status, results.Items, results.TotalCount, next));
        }
        await transaction.CommitAsync(cancellationToken);
        return new ProposalKanbanDto(pageSizePerColumn, columns.Sum(value => value.TotalCount), columns);
    }

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 1;
        try
        {
            var value = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(value, out var page) && page >= 1 && page <= int.MaxValue / 100 ? page : throw new FormatException();
        }
        catch (FormatException) { throw new ArgumentException("Cursor inválido.", nameof(cursor)); }
    }

    public async Task<ProposalDetailsDto?> FindAsync(Guid organizationId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.CommercialRequests.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.Id == requestId)
            .Select(value => new
            {
                value.Id, value.CustomerId, value.ProspectName, value.ProspectContactName, value.ProspectEmail,
                value.ProspectPhone, value.Description, value.Origin, value.Status, value.Version, value.OutcomeReason,
                value.Number, value.ResponsibleUserId, value.CreatedAtUtc, value.UpdatedAtUtc,
                value.CustomerDeadline, value.InternalDeadline, value.ReminderOccurrenceRevision,
                value.ReminderOccurrenceId, value.NextActionDescription, value.NextActionAtUtc
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
        var activities = await (
            from activity in dbContext.CommercialActivities.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on new { activity.OrganizationId, activity.ActorUserId }
                equals new { user.OrganizationId, ActorUserId = user.Id }
            where activity.OrganizationId == organizationId && activity.CommercialRequestId == requestId
            orderby activity.OccurredAtUtc, activity.Id
            select new ProposalActivityDto(activity.Id, activity.ActorUserId, user.DisplayName,
                activity.EventType, activity.Description, activity.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new ProposalDetailsDto(request.Id, request.CustomerId, customerName, request.ProspectName,
            request.ProspectContactName, request.ProspectEmail, request.ProspectPhone, request.Number,
            request.ResponsibleUserId, responsibleName, references, request.Description,
            request.Origin, request.Status, request.Version, revisions.FirstOrDefault()?.Number,
            revisions, activities, request.CreatedAtUtc, request.UpdatedAtUtc, request.OutcomeReason,
            draft?.State, draft?.ConvertedDemandId, request.CustomerDeadline, request.InternalDeadline,
            request.ReminderOccurrenceRevision, request.ReminderOccurrenceId is null ? null : new ProposalNextActionDto(
                request.ReminderOccurrenceId.Value, request.ReminderOccurrenceRevision,
                request.NextActionDescription!, request.NextActionAtUtc!.Value));
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
            AddActivity(request, actorUserId, "RequestCreated", "Solicitação comercial registrada.", now);
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
            AddActivity(request, actorUserId, "RequestUpdated", "Dados da solicitação comercial atualizados.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
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

    public async Task<ProposalCommandResult> ChangeStatusAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, ProposalOutcomeInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var request = await dbContext.CommercialRequests.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version) return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        var demandDraftConverted = await dbContext.DemandDrafts.AnyAsync(value => value.OrganizationId == organizationId
            && value.ProposalRequestId == requestId && value.State == DemandDraft.Converted, cancellationToken);
        if (input.Status is not (CommercialRequest.Evaluating or CommercialRequest.Negotiating
            or CommercialRequest.NotQuoted or CommercialRequest.Rejected or CommercialRequest.Expired or CommercialRequest.Cancelled))
            return new ProposalCommandResult(null, ProposalFailure.InvalidData);
        try
        {
            request.ChangeStatus(input.Status, input.Reason, now, demandDraftConverted);
            AddActivity(request, actorUserId, "StatusChanged", $"Etapa alterada para {input.Status}.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
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
        catch (CommercialResponsibleRequiredException)
        {
            return new ProposalCommandResult(null, ProposalFailure.ResponsibleRequired);
        }
        catch (InvalidOperationException)
        {
            return new ProposalCommandResult(null, ProposalFailure.InvalidState);
        }
    }

    public async Task<ProposalCommandResult> CreateRevisionAsync(Guid organizationId, Guid requestId,
        Guid actorUserId, CreateProposalRevisionInput input, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var request = await dbContext.CommercialRequests.Include(value => value.Revisions)
                .ThenInclude(value => value.Items)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version) return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        var converted = await dbContext.DemandDrafts.AnyAsync(value => value.OrganizationId == organizationId
            && value.ProposalRequestId == requestId && value.State == "Converted", cancellationToken);
        try
        {
            var revision = request.StartRevision(Guid.NewGuid(), actorUserId,
                input.Items!.Select(item => (item.Description!, item.Amount)), input.ValidUntil, input.Notes, now, converted);
            dbContext.ProposalRevisions.Add(revision);
            AddActivity(request, actorUserId, "RevisionCreated",
                $"Revisão {revision.Number} criada.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
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
        var request = await dbContext.CommercialRequests.Include(value => value.Revisions)
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.Id == requestId, cancellationToken);
        if (request is null) return new ProposalCommandResult(null, ProposalFailure.NotFound);
        if (request.Version != input.Version) return new ProposalCommandResult(null, ProposalFailure.VersionConflict);
        try
        {
            request.RecordSent(input.RevisionId, input.Channel!, input.SentAtUtc, now);
            AddActivity(request, actorUserId, "ProposalSent", $"Revisão enviada pelo canal {input.Channel}.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
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
            request.RecordApproval(input.RevisionId, actorUserId, input.ApproverName!, input.Channel!,
                input.Notes, input.ApprovedAtUtc, now);
            AddActivity(request, actorUserId, "ProposalApproved", $"Proposta aprovada pelo cliente ({input.Channel}).", now);
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

    private void AddActivity(CommercialRequest request, Guid actorUserId, string eventType,
        string description, DateTimeOffset now) => dbContext.CommercialActivities.Add(new CommercialActivity(
        Guid.NewGuid(), request.OrganizationId, request.Id, actorUserId, eventType, description, now));

    private static (string Type, string Value)[] ToReferenceValues(IReadOnlyList<CommercialReferenceInput>? references)
    {
        if (references is null) return [];
        return references.Where(value => !string.IsNullOrWhiteSpace(value.Type) || !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => (value.Type?.Trim() ?? "", value.Value?.Trim() ?? "")).ToArray();
    }

    private static string EscapeLike(string term) => term.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
