using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Demands;
using PitCrm.Domain.Demands;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Demands;

public sealed class PersistenceDemandStore(PitCrmDbContext dbContext) : IDemandStore
{
    public async Task<DemandCreateResult> CreateAsync(Demand demand, DemandActivity initialActivity, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await dbContext.Customers.AnyAsync(customer => customer.OrganizationId == demand.OrganizationId
                && customer.Id == demand.CustomerId, cancellationToken))
            return new DemandCreateResult(null, DemandCreateFailure.CustomerNotFound);
        if (!await dbContext.CustomerLocations.AnyAsync(location => location.OrganizationId == demand.OrganizationId
                && location.CustomerId == demand.CustomerId && location.Id == demand.LocationId, cancellationToken))
            return new DemandCreateResult(null, DemandCreateFailure.LocationNotFound);
        if (demand.AssignedToUserId is { } assigneeId && !await dbContext.Users.AnyAsync(user => user.OrganizationId == demand.OrganizationId
                && user.Id == assigneeId && user.IsActive, cancellationToken))
            return new DemandCreateResult(null, DemandCreateFailure.AssigneeNotEligible);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DemandNumberCounters" ("OrganizationId", "LastIssuedNumber")
            VALUES ({demand.OrganizationId}, 1)
            ON CONFLICT ("OrganizationId") DO UPDATE
            SET "LastIssuedNumber" = "DemandNumberCounters"."LastIssuedNumber" + 1;
            """, cancellationToken);
        var referenceNumber = await dbContext.Database.SqlQuery<int>($"""
            SELECT "LastIssuedNumber" AS "Value"
            FROM "DemandNumberCounters"
            WHERE "OrganizationId" = {demand.OrganizationId}
            """).SingleAsync(cancellationToken);
        demand.AssignReferenceNumber(referenceNumber);

        dbContext.Demands.Add(demand);
        dbContext.DemandActivities.Add(initialActivity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DemandCreateResult(demand, DemandCreateFailure.None);
    }

    public async Task<DemandSearchResult> SearchAsync(Guid organizationId, string? searchTerm, Guid? customerId,
        Guid? locationId, Guid? assignedToUserId, string? status, string? priority, string sortBy,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var demands = dbContext.Demands.AsNoTracking()
            .Where(demand => demand.OrganizationId == organizationId);
        if (customerId is { } customerFilter) demands = demands.Where(demand => demand.CustomerId == customerFilter);
        if (locationId is { } locationFilter) demands = demands.Where(demand => demand.LocationId == locationFilter);
        if (assignedToUserId is { } assigneeFilter) demands = demands.Where(demand => demand.AssignedToUserId == assigneeFilter);
        if (!string.IsNullOrWhiteSpace(status)) demands = demands.Where(demand => demand.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) demands = demands.Where(demand => demand.Priority == priority);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            var escapedTerm = term.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escapedTerm}%";
            var isDemandId = Guid.TryParse(term, out var demandId);
            var referenceText = term.StartsWith("DEM-", StringComparison.OrdinalIgnoreCase) ? term[4..] : term;
            var isReferenceNumber = int.TryParse(referenceText, out var referenceNumber);
            demands = demands.Where(demand =>
                (isDemandId && demand.Id == demandId)
                || (isReferenceNumber && demand.ReferenceNumber == referenceNumber)
                || EF.Functions.ILike(demand.Description, pattern, "\\")
                || (demand.TicketReference != null && EF.Functions.ILike(demand.TicketReference, pattern, "\\"))
                || (demand.PurchaseOrderReference != null && EF.Functions.ILike(demand.PurchaseOrderReference, pattern, "\\")));
        }

        var totalCount = await demands.CountAsync(cancellationToken);
        var itemsQuery =
            from demand in demands
            join customer in dbContext.Customers.AsNoTracking()
                on new { demand.OrganizationId, demand.CustomerId } equals new { customer.OrganizationId, CustomerId = customer.Id }
            join location in dbContext.CustomerLocations.AsNoTracking()
                on new { demand.OrganizationId, demand.CustomerId, demand.LocationId }
                equals new { location.OrganizationId, location.CustomerId, LocationId = location.Id }
            join assignee in dbContext.Users.AsNoTracking()
                on new { demand.OrganizationId, demand.AssignedToUserId }
                equals new { assignee.OrganizationId, AssignedToUserId = (Guid?)assignee.Id } into assignees
            from assignee in assignees.DefaultIfEmpty()
            select new { Demand = demand, CustomerName = customer.Name, LocationName = location.Name,
                AssigneeName = assignee == null ? null : assignee.DisplayName };
        var orderedItems = sortBy == "priority"
            ? itemsQuery.OrderByDescending(item => item.Demand.Priority == "Alta" ? 3 : item.Demand.Priority == "Média" ? 2 : item.Demand.Priority == "Baixa" ? 1 : 0)
                .ThenByDescending(item => item.Demand.CreatedAtUtc).ThenByDescending(item => item.Demand.Id)
            : itemsQuery.OrderByDescending(item => item.Demand.CreatedAtUtc).ThenByDescending(item => item.Demand.Id);
        var items = await orderedItems.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new DemandListItemDto(item.Demand.Id, item.Demand.ReferenceNumber,
                item.Demand.CustomerId, item.CustomerName, item.Demand.LocationId, item.LocationName,
                item.Demand.Description, item.Demand.Origin, item.Demand.Priority, item.Demand.Status,
                item.Demand.TicketReference, item.Demand.PurchaseOrderReference, item.Demand.AssignedToUserId,
                item.AssigneeName, item.Demand.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new DemandSearchResult(items, totalCount);
    }

    public async Task<DemandDetailsDto?> FindAsync(Guid organizationId, Guid demandId, CancellationToken cancellationToken)
    {
        var demand = await dbContext.Demands.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.Id == demandId)
            .Select(item => new
            {
                item.Id, item.ReferenceNumber, item.CustomerId, item.LocationId, item.Description, item.Origin, item.Priority,
                item.Status, item.TicketReference, item.PurchaseOrderReference, item.AssignedToUserId,
                item.CreatedByUserId, item.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (demand is null) return null;

        var customerName = await dbContext.Customers.AsNoTracking()
            .Where(customer => customer.OrganizationId == organizationId && customer.Id == demand.CustomerId)
            .Select(customer => customer.Name)
            .SingleAsync(cancellationToken);
        var locationName = await dbContext.CustomerLocations.AsNoTracking()
            .Where(location => location.OrganizationId == organizationId && location.Id == demand.LocationId)
            .Select(location => location.Name)
            .SingleAsync(cancellationToken);
        var createdByDisplayName = await dbContext.Users.AsNoTracking()
            .Where(user => user.OrganizationId == organizationId && user.Id == demand.CreatedByUserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);
        var assignedToDisplayName = demand.AssignedToUserId is { } assigneeId
            ? await dbContext.Users.AsNoTracking()
                .Where(user => user.OrganizationId == organizationId && user.Id == assigneeId)
                .Select(user => user.DisplayName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var activities = await (
            from activity in dbContext.DemandActivities.AsNoTracking()
            join author in dbContext.Users.AsNoTracking()
                on new { activity.OrganizationId, activity.AuthorUserId }
                equals new { author.OrganizationId, AuthorUserId = author.Id }
            where activity.OrganizationId == organizationId && activity.DemandId == demandId
            orderby activity.OccurredAtUtc, activity.Id
            select new DemandActivityDto(activity.Id, activity.AuthorUserId, author.DisplayName,
                activity.EventType, activity.Description, activity.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new DemandDetailsDto(demand.Id, demand.ReferenceNumber, demand.CustomerId, customerName, demand.LocationId,
            locationName, demand.Description, demand.Origin, demand.Priority, demand.Status,
            demand.TicketReference, demand.PurchaseOrderReference, demand.AssignedToUserId,
            assignedToDisplayName, demand.CreatedByUserId, createdByDisplayName, demand.CreatedAtUtc, activities);
    }
}
