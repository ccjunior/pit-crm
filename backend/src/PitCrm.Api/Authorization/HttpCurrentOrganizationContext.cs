using System.Security.Claims;
using PitCrm.Application.Authorization;
using PitCrm.Infrastructure.Identity;

namespace PitCrm.Api.Authorization;

public sealed class HttpCurrentOrganizationContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentOrganizationContext
{
    public Guid UserId => GetRequiredGuid(ClaimTypes.NameIdentifier);

    public Guid OrganizationId => GetRequiredGuid(OrganizationIdClaim.Type);

    private Guid GetRequiredGuid(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            throw new InvalidOperationException("An authenticated organization context is required.");
        }

        return id;
    }
}
