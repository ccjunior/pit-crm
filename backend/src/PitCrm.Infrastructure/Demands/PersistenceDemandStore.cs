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

        dbContext.Demands.Add(demand);
        dbContext.DemandActivities.Add(initialActivity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DemandCreateResult(demand, DemandCreateFailure.None);
    }
}
