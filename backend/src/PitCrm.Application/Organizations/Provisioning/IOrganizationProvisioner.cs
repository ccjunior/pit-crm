namespace PitCrm.Application.Organizations.Provisioning;

public interface IOrganizationProvisioner
{
    Task<OrganizationProvisioningResult> ProvisionAsync(
        OrganizationProvisioningRequest request,
        CancellationToken cancellationToken = default);
}
