using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Authorization;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Authorization;

public sealed class PersistenceOrganizationPermissionGrantLookup(PitCrmDbContext dbContext)
    : IOrganizationPermissionGrantLookup
{
    public Task<bool> HasGrantAsync(Guid organizationId, Guid userId, string permission,
        CancellationToken cancellationToken) => dbContext.OrganizationPermissionGrants.AsNoTracking()
        .AnyAsync(value => value.OrganizationId == organizationId && value.UserId == userId
            && value.Permission == permission, cancellationToken);
}
