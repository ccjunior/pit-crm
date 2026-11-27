namespace PitCrm.Domain.Organizations;

public sealed class OrganizationPermissionGrant
{
    private OrganizationPermissionGrant() { }

    public OrganizationPermissionGrant(Guid organizationId, Guid userId, string permission)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Permission grant scope is invalid.");
        if (string.IsNullOrWhiteSpace(permission) || permission.Length > 100)
            throw new ArgumentOutOfRangeException(nameof(permission));

        OrganizationId = organizationId;
        UserId = userId;
        Permission = permission;
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
}
