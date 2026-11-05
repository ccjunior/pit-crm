namespace PitCrm.Application.Organizations.Provisioning;

public sealed class OrganizationProvisioningRequest
{
    public Guid OrganizationId { get; init; }

    public string OrganizationName { get; init; } = string.Empty;

    public string AdministratorDisplayName { get; init; } = string.Empty;

    public string AdministratorEmail { get; init; } = string.Empty;

    public string AdministratorPassword { get; init; } = string.Empty;
}
