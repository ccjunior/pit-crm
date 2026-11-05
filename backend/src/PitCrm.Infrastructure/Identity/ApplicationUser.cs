using Microsoft.AspNetCore.Identity;
using PitCrm.Domain.Organizations;

namespace PitCrm.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }

    public bool IsActive { get; set; } = true;

    public Organization Organization { get; set; } = null!;
}
