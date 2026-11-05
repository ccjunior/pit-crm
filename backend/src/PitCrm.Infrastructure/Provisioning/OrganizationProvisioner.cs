using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Authorization;
using PitCrm.Application.Organizations.Provisioning;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Infrastructure.Provisioning;

public sealed class OrganizationProvisioner(
    PitCrmDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
    : IOrganizationProvisioner
{
    public async Task<OrganizationProvisioningResult> ProvisionAsync(
        OrganizationProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var administratorEmail = request.AdministratorEmail.Trim();
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(value => value.Id == request.OrganizationId, cancellationToken);
        var existingUser = await userManager.FindByEmailAsync(administratorEmail);

        if (organization is not null || existingUser is not null)
        {
            var isAlreadyProvisioned = organization is not null
                && existingUser is not null
                && existingUser.OrganizationId == organization.Id
                && existingUser.IsActive
                && await userManager.IsInRoleAsync(existingUser, RoleNames.Administrator);

            if (!isAlreadyProvisioned)
            {
                throw new InvalidOperationException(
                    "Provisioning conflicts with existing records. No data was changed.");
            }

            await transaction.RollbackAsync(cancellationToken);
            return new OrganizationProvisioningResult(
                OrganizationProvisioningStatus.AlreadyProvisioned,
                organization!.Id);
        }

        organization = new Organization(request.OrganizationId, request.OrganizationName);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);

        var administratorRole = await roleManager.FindByNameAsync(RoleNames.Administrator);
        if (administratorRole is null)
        {
            var createRoleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(RoleNames.Administrator));
            EnsureSuccess(createRoleResult);
        }

        var administrator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = request.AdministratorDisplayName.Trim(),
            OrganizationId = organization.Id,
            UserName = administratorEmail,
            Email = administratorEmail,
            IsActive = true
        };

        EnsureSuccess(await userManager.CreateAsync(
            administrator,
            request.AdministratorPassword));
        EnsureSuccess(await userManager.AddToRoleAsync(
            administrator,
            RoleNames.Administrator));

        await transaction.CommitAsync(cancellationToken);

        return new OrganizationProvisioningResult(
            OrganizationProvisioningStatus.Created,
            organization.Id);
    }

    private static void Validate(OrganizationProvisioningRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("A stable organization ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizationName)
            || request.OrganizationName.Trim().Length > 200)
        {
            throw new ArgumentException("Organization name is required and limited to 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.AdministratorDisplayName)
            || request.AdministratorDisplayName.Trim().Length > 200)
        {
            throw new ArgumentException("Administrator name is required and limited to 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.AdministratorEmail))
        {
            throw new ArgumentException("Administrator email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AdministratorPassword))
        {
            throw new ArgumentException("Administrator password is required.");
        }
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            var errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException(
                $"Identity rejected the provisioning request ({errorCodes}). No data was committed.");
        }
    }
}
