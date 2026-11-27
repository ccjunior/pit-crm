using Microsoft.AspNetCore.Authorization;
using PitCrm.Application.Authorization;

namespace PitCrm.Api.Authorization;

public sealed record OrganizationPermissionGrantRequirement(string Permission) : IAuthorizationRequirement;

public sealed class OrganizationPermissionGrantHandler(
    ICurrentOrganizationContext organizationContext,
    IOrganizationPermissionGrantLookup grantLookup,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OrganizationPermissionGrantRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OrganizationPermissionGrantRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        Guid organizationId;
        Guid userId;
        try
        {
            organizationId = organizationContext.OrganizationId;
            userId = organizationContext.UserId;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await grantLookup.HasGrantAsync(organizationId, userId, requirement.Permission, cancellationToken))
            context.Succeed(requirement);
    }
}
