namespace PitCrm.Application.Organizations.Provisioning;

public enum OrganizationProvisioningStatus
{
    Created,
    AlreadyProvisioned
}

public sealed record OrganizationProvisioningResult(
    OrganizationProvisioningStatus Status,
    Guid OrganizationId);
