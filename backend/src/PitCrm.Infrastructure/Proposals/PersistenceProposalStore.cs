using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Security.Cryptography;
using System.Text;
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
        Guid? customerId, Guid? responsibleUserId, string? origin, string? overdueFor,
        DateOnly? from, DateOnly? to, DateOnly? customerDeadlineFrom, DateOnly? customerDeadlineTo,
        DateOnly? internalDeadlineFrom, DateOnly? internalDeadlineTo,
        DateTimeOffset? nextActionFromUtc, DateTimeOffset? nextActionToUtc, string sortBy, bool sortAscending,
        int page, int pageSize, DateOnly today, CancellationToken cancellationToken)
    {
        var result = await SearchCoreAsync(organizationId, searchTerm, status, customerId, responsibleUserId,
            origin, overdueFor, from, to, customerDeadlineFrom, customerDeadlineTo, internalDeadlineFrom,
            internalDeadlineTo, nextActionFromUtc, nextActionToUtc, sortBy, sortAscending,
            page, pageSize, today, null, false, cancellationToken);
        return result.Page;
    }

    private async Task<SearchPageWindow> SearchCoreAsync(Guid organizationId, string? searchTerm, string? status,
        Guid? customerId, Guid? responsibleUserId, string? origin, string? overdueFor,
        DateOnly? from, DateOnly? to, DateOnly? customerDeadlineFrom, DateOnly? customerDeadlineTo,
        DateOnly? internalDeadlineFrom, DateOnly? internalDeadlineTo,
        DateTimeOffset? nextActionFromUtc, DateTimeOffset? nextActionToUtc, string sortBy, bool sortAscending,
        int page, int pageSize, DateOnly today, KanbanCursor? seekCursor, bool includeHasMore,
        CancellationToken cancellationToken)
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
        if (customerDeadlineFrom is { } customerStart) requests = requests.Where(value => value.CustomerDeadline >= customerStart);
        if (customerDeadlineTo is { } customerEnd) requests = requests.Where(value => value.CustomerDeadline <= customerEnd);
        if (internalDeadlineFrom is { } internalStart) requests = requests.Where(value => value.InternalDeadline >= internalStart);
        if (internalDeadlineTo is { } internalEnd) requests = requests.Where(value => value.InternalDeadline <= internalEnd);
        if (nextActionFromUtc is { } actionStart) requests = requests.Where(value => value.NextActionAtUtc >= actionStart);
        if (nextActionToUtc is { } actionEnd) requests = requests.Where(value => value.NextActionAtUtc < actionEnd);
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
        var selectedRequests = seekCursor is null ? requests : ApplySeekCursor(requests, sortBy, sortAscending, seekCursor);
        var itemsQuery =
            from request in selectedRequests
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
            "customerDeadline" => sortAscending ? itemsQuery.OrderBy(value => value.Request.CustomerDeadline == null)
                    .ThenBy(value => value.Request.CustomerDeadline).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderBy(value => value.Request.CustomerDeadline == null)
                    .ThenByDescending(value => value.Request.CustomerDeadline).ThenBy(value => value.Request.Id),
            "internalDeadline" => sortAscending ? itemsQuery.OrderBy(value => value.Request.InternalDeadline == null)
                    .ThenBy(value => value.Request.InternalDeadline).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderBy(value => value.Request.InternalDeadline == null)
                    .ThenByDescending(value => value.Request.InternalDeadline).ThenBy(value => value.Request.Id),
            "nextActionAtUtc" => sortAscending ? itemsQuery.OrderBy(value => value.Request.NextActionAtUtc == null)
                    .ThenBy(value => value.Request.NextActionAtUtc).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderBy(value => value.Request.NextActionAtUtc == null)
                    .ThenByDescending(value => value.Request.NextActionAtUtc).ThenBy(value => value.Request.Id),
            "number" => sortAscending ? itemsQuery.OrderBy(value => value.Request.Number).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.Number).ThenBy(value => value.Request.Id),
            _ => sortAscending ? itemsQuery.OrderBy(value => value.Request.UpdatedAtUtc).ThenBy(value => value.Request.Id)
                : itemsQuery.OrderByDescending(value => value.Request.UpdatedAtUtc).ThenByDescending(value => value.Request.Id),
        };
        var rowLimit = includeHasMore ? pageSize + 1 : pageSize;
        var selectedItems = seekCursor is null ? orderedItems.Skip((page - 1) * pageSize) : orderedItems;
        var projectedRows = await selectedItems.Take(rowLimit)
            .Select(value => new ProposalListItemDto(value.Request.Id, value.Request.CustomerId,
                value.CustomerName, value.Request.ProspectName, value.Request.Number, value.Request.ResponsibleUserId,
                value.ResponsibleName, value.Request.Description, value.Request.Origin,
                value.Request.Status, value.Request.Version, value.RevisionNumber, value.RevisionTotal,
                value.Request.UpdatedAtUtc, value.Request.CustomerDeadline, value.Request.InternalDeadline,
                value.Request.ReminderOccurrenceId == null ? null : new ProposalNextActionDto(
                    value.Request.ReminderOccurrenceId.Value, value.Request.ReminderOccurrenceRevision,
                    value.Request.NextActionDescription!, value.Request.NextActionAtUtc!.Value),
                ProposalDeadlineOverdueDto.Calculate(value.Request.CustomerDeadline,
                    value.Request.InternalDeadline, today)))
            .ToListAsync(cancellationToken);
        var hasMore = includeHasMore && projectedRows.Count > pageSize;
        IReadOnlyList<ProposalListItemDto> rows = hasMore ? projectedRows.Take(pageSize).ToArray() : projectedRows;
        if (snapshot is not null) await snapshot.CommitAsync(cancellationToken);
        return new SearchPageWindow(new ProposalPageDto(rows, page, pageSize, totalCount), hasMore);
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
                value.InternalDeadline, ProposalDeadlineOverdueDto.Calculate(value.CustomerDeadline,
                    value.InternalDeadline, today).Either,
                new ProposalNextActionDto(value.ReminderOccurrenceId!.Value, value.ReminderOccurrenceRevision,
                    value.NextActionDescription!, value.NextActionAtUtc!.Value),
                ProposalDeadlineOverdueDto.Calculate(value.CustomerDeadline, value.InternalDeadline, today)))
            .ToListAsync(cancellationToken);
        return new MyActionsPageDto(rows, page, pageSize, count);
    }

    public async Task<ProposalKanbanDto> KanbanAsync(Guid organizationId, string? searchTerm, Guid? customerId,
        Guid? responsibleUserId, string? origin, string? overdueFor, DateOnly? from, DateOnly? to,
        DateOnly? customerDeadlineFrom, DateOnly? customerDeadlineTo,
        DateOnly? internalDeadlineFrom, DateOnly? internalDeadlineTo,
        DateTimeOffset? nextActionFromUtc, DateTimeOffset? nextActionToUtc,
        string sortBy, bool sortAscending, int pageSizePerColumn, IReadOnlyDictionary<string, string?> cursors, DateOnly today,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var filterFingerprint = CreateFilterFingerprint(organizationId, searchTerm, customerId, responsibleUserId,
            origin, overdueFor, from, to, customerDeadlineFrom, customerDeadlineTo, internalDeadlineFrom,
            internalDeadlineTo, nextActionFromUtc, nextActionToUtc, sortBy, sortAscending, today);
        var columns = new List<ProposalKanbanColumnDto>();
        foreach (var status in CommercialRequest.Statuses)
        {
            var cursor = DecodeCursor(cursors.GetValueOrDefault(status), status, sortBy, sortAscending, filterFingerprint);
            var results = await SearchCoreAsync(organizationId, searchTerm, status, customerId, responsibleUserId,
                origin, overdueFor, from, to, customerDeadlineFrom, customerDeadlineTo,
                internalDeadlineFrom, internalDeadlineTo, nextActionFromUtc, nextActionToUtc,
                sortBy, sortAscending, 1, pageSizePerColumn, today, cursor, true, cancellationToken);
            var next = results.HasMore && results.Page.Items.Count > 0
                ? EncodeCursor(CreateCursor(status, sortBy, sortAscending, filterFingerprint, results.Page.Items[^1]))
                : null;
            columns.Add(new ProposalKanbanColumnDto(status, results.Page.Items, results.Page.TotalCount, next));
        }
        await transaction.CommitAsync(cancellationToken);
        return new ProposalKanbanDto(pageSizePerColumn, columns.Sum(value => value.TotalCount), columns);
    }

    private static KanbanCursor? DecodeCursor(string? token, string status, string sortBy,
        bool sortAscending, string filterFingerprint)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var encoded = token.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + (4 - encoded.Length % 4) % 4, '=');
            var cursor = JsonSerializer.Deserialize<KanbanCursor>(Convert.FromBase64String(encoded),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cursor is null || cursor.Version != 1 || cursor.Status != status || cursor.SortBy != sortBy
                || cursor.SortAscending != sortAscending || cursor.FilterFingerprint != filterFingerprint
                || cursor.LastId == Guid.Empty || !IsCursorValueValid(cursor))
                throw new FormatException();
            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        { throw new ArgumentException("Cursor inválido ou incompatível com lane/filtros/ordenação.", nameof(token)); }
    }

    private static bool IsCursorValueValid(KanbanCursor cursor) => cursor.SortBy switch
    {
        "updatedAtUtc" => DateTimeOffset.TryParse(cursor.LastValue, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _),
        "customerDeadline" or "internalDeadline" or "nextActionAtUtc" => cursor.LastValue is null
            || (cursor.SortBy == "nextActionAtUtc"
                ? DateTimeOffset.TryParse(cursor.LastValue, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out _)
                : DateOnly.TryParseExact(cursor.LastValue, "yyyy-MM-dd", out _)),
        "number" => !string.IsNullOrEmpty(cursor.LastValue),
        _ => false,
    };

    private static IQueryable<CommercialRequest> ApplySeekCursor(IQueryable<CommercialRequest> requests,
        string sortBy, bool ascending, KanbanCursor cursor)
    {
        var idAscending = sortBy != "updatedAtUtc" || ascending;
        var id = cursor.LastId;
        return sortBy switch
        {
            "updatedAtUtc" => SeekUpdatedAt(requests, cursor, ascending, idAscending),
            "customerDeadline" => SeekNullableDate(requests, nameof(CommercialRequest.CustomerDeadline), cursor, ascending),
            "internalDeadline" => SeekNullableDate(requests, nameof(CommercialRequest.InternalDeadline), cursor, ascending),
            "nextActionAtUtc" => SeekNullableInstant(requests, cursor, ascending),
            "number" => SeekNumber(requests, cursor, ascending),
            _ => throw new ArgumentException("Unsupported cursor sort."),
        };
    }

    private static IQueryable<CommercialRequest> SeekUpdatedAt(IQueryable<CommercialRequest> query,
        KanbanCursor cursor, bool ascending, bool idAscending)
    {
        var value = DateTimeOffset.Parse(cursor.LastValue!, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);
        if (ascending && idAscending)
            return query.Where(request => request.UpdatedAtUtc > value || (request.UpdatedAtUtc == value && request.Id.CompareTo(cursor.LastId) > 0));
        if (ascending)
            return query.Where(request => request.UpdatedAtUtc > value || (request.UpdatedAtUtc == value && request.Id.CompareTo(cursor.LastId) < 0));
        if (idAscending)
            return query.Where(request => request.UpdatedAtUtc < value || (request.UpdatedAtUtc == value && request.Id.CompareTo(cursor.LastId) > 0));
        return query.Where(request => request.UpdatedAtUtc < value || (request.UpdatedAtUtc == value && request.Id.CompareTo(cursor.LastId) < 0));
    }

    private static IQueryable<CommercialRequest> SeekNullableDate(IQueryable<CommercialRequest> query,
        string propertyName,
        KanbanCursor cursor, bool ascending)
    {
        var value = cursor.LastValue is null ? (DateOnly?)null : DateOnly.ParseExact(cursor.LastValue, "yyyy-MM-dd");
        if (value is null)
            return query.Where(request => EF.Property<DateOnly?>(request, propertyName) == null
                && request.Id.CompareTo(cursor.LastId) > 0);
        var lastValue = value.Value;
        if (ascending)
            return query.Where(request => EF.Property<DateOnly?>(request, propertyName) == null
                || EF.Property<DateOnly?>(request, propertyName) > lastValue
                || (EF.Property<DateOnly?>(request, propertyName) == lastValue && request.Id.CompareTo(cursor.LastId) > 0));
        return query.Where(request => EF.Property<DateOnly?>(request, propertyName) == null
            || EF.Property<DateOnly?>(request, propertyName) < lastValue
            || (EF.Property<DateOnly?>(request, propertyName) == lastValue && request.Id.CompareTo(cursor.LastId) > 0));
    }

    private static IQueryable<CommercialRequest> SeekNullableInstant(IQueryable<CommercialRequest> query,
        KanbanCursor cursor, bool ascending)
    {
        var value = cursor.LastValue is null ? (DateTimeOffset?)null : DateTimeOffset.Parse(cursor.LastValue,
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        if (value is null)
            return query.Where(request => EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) == null
                && request.Id.CompareTo(cursor.LastId) > 0);
        var lastValue = value.Value;
        if (ascending)
            return query.Where(request => EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) == null
                || EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) > lastValue
                || (EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) == lastValue && request.Id.CompareTo(cursor.LastId) > 0));
        return query.Where(request => EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) == null
            || EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) < lastValue
            || (EF.Property<DateTimeOffset?>(request, nameof(CommercialRequest.NextActionAtUtc)) == lastValue && request.Id.CompareTo(cursor.LastId) > 0));
    }

    private static IQueryable<CommercialRequest> SeekNumber(IQueryable<CommercialRequest> query,
        KanbanCursor cursor, bool ascending)
    {
        var value = cursor.LastValue!;
        return query.Where(request => ascending
            ? request.Number.CompareTo(value) > 0 || (request.Number == value && request.Id.CompareTo(cursor.LastId) > 0)
            : request.Number.CompareTo(value) < 0 || (request.Number == value && request.Id.CompareTo(cursor.LastId) > 0));
    }

    private static KanbanCursor CreateCursor(string status, string sortBy, bool sortAscending,
        string filterFingerprint, ProposalListItemDto item)
    {
        var sortValue = sortBy switch
        {
            "updatedAtUtc" => item.UpdatedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "customerDeadline" => item.CustomerDeadline?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            "internalDeadline" => item.InternalDeadline?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            "nextActionAtUtc" => item.NextAction?.ScheduledAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "number" => item.Number,
            _ => throw new ArgumentException("Unsupported cursor sort.", nameof(sortBy)),
        };
        return new KanbanCursor(1, status, sortBy, sortAscending, filterFingerprint, sortValue, item.Id);
    }

    private static string CreateFilterFingerprint(Guid organizationId, string? searchTerm, Guid? customerId,
        Guid? responsibleUserId, string? origin, string? overdueFor, DateOnly? from, DateOnly? to,
        DateOnly? customerDeadlineFrom, DateOnly? customerDeadlineTo, DateOnly? internalDeadlineFrom,
        DateOnly? internalDeadlineTo, DateTimeOffset? nextActionFromUtc, DateTimeOffset? nextActionToUtc,
        string sortBy, bool sortAscending, DateOnly today)
    {
        var filter = JsonSerializer.Serialize(new { organizationId, searchTerm = searchTerm?.Trim(), customerId,
            responsibleUserId, origin, overdueFor, from, to, customerDeadlineFrom, customerDeadlineTo,
            internalDeadlineFrom, internalDeadlineTo, nextActionFromUtc, nextActionToUtc, sortBy, sortAscending, today });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(filter)));
    }

    private static string EncodeCursor(KanbanCursor cursor)
    {
        var base64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor));
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
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
                request.NextActionDescription!, request.NextActionAtUtc!.Value),
            ProposalDeadlineOverdueDto.Calculate(request.CustomerDeadline, request.InternalDeadline,
                ProposalDateRules.Today(DateTimeOffset.UtcNow)));
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

    private sealed record SearchPageWindow(ProposalPageDto Page, bool HasMore);
    private sealed record KanbanCursor(int Version, string Status, string SortBy, bool SortAscending,
        string FilterFingerprint, string? LastValue, Guid LastId);

    private static string EscapeLike(string term) => term.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
