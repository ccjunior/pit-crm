using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Users.EligibleAssignees;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Users.EligibleAssignees;

public sealed class EligibleAssigneeQuery(PitCrmDbContext dbContext) : IEligibleAssigneeQuery
{
    public async Task<IReadOnlyList<EligibleAssignee>> ExecuteAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID is required.", nameof(organizationId));
        }

        return await dbContext.Users.AsNoTracking()
            .Where(user => user.OrganizationId == organizationId && user.IsActive)
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Select(user => new EligibleAssignee(user.Id, user.DisplayName))
            .ToListAsync(cancellationToken);
    }
}
