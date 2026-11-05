using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PitCrm.Application.Authorization;
using PitCrm.Application.Organizations.Provisioning;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;
using PitCrm.Infrastructure.Provisioning;

namespace PitCrm.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class OrganizationProvisioningTests
{
    [Fact]
    public async Task Repeating_provisioning_keeps_the_original_account_and_password()
    {
        var connectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"provisioning_{Guid.NewGuid():N}";
        var originalConnection = new NpgsqlConnectionStringBuilder(connectionString);
        var adminConnection = new NpgsqlConnectionStringBuilder(originalConnection.ConnectionString)
        {
            SearchPath = "public"
        };
        var isolatedConnection = new NpgsqlConnectionStringBuilder(originalConnection.ConnectionString)
        {
            SearchPath = schemaName
        };

        await using (var connection = new NpgsqlConnection(adminConnection.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<PitCrmDbContext>(options =>
                options.UseNpgsql(isolatedConnection.ConnectionString));
            services.AddIdentityCore<ApplicationUser>(options =>
                options.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<PitCrmDbContext>();
            services.AddScoped<IOrganizationProvisioner, OrganizationProvisioner>();

            using var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            var organizationId = Guid.NewGuid();
            var originalPassword = $"Initial-{Guid.NewGuid():N}-Aa1!";
            var repeatPassword = $"Changed-{Guid.NewGuid():N}-Bb2!";
            var request = new OrganizationProvisioningRequest
            {
                OrganizationId = organizationId,
                OrganizationName = "PIT-CRM Teste",
                AdministratorDisplayName = "Administrador de Teste",
                AdministratorEmail = $"admin-{organizationId:N}@example.invalid",
                AdministratorPassword = originalPassword
            };

            OrganizationProvisioningResult firstResult;
            string originalPasswordHash;
            using (var scope = provider.CreateScope())
            {
                var provisioner = scope.ServiceProvider
                    .GetRequiredService<IOrganizationProvisioner>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                firstResult = await provisioner.ProvisionAsync(request);
                var user = await userManager.FindByEmailAsync(request.AdministratorEmail);

                Assert.NotNull(user);
                Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Administrator));
                Assert.True(await userManager.CheckPasswordAsync(user, originalPassword));
                originalPasswordHash = user.PasswordHash!;
            }

            var repeatRequest = new OrganizationProvisioningRequest
            {
                OrganizationId = organizationId,
                OrganizationName = "Nome alterado não deve substituir a organização existente",
                AdministratorDisplayName = "Nome alterado não deve substituir a conta existente",
                AdministratorEmail = request.AdministratorEmail,
                AdministratorPassword = repeatPassword
            };

            using (var scope = provider.CreateScope())
            {
                var provisioner = scope.ServiceProvider
                    .GetRequiredService<IOrganizationProvisioner>();
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var repeatResult = await provisioner.ProvisionAsync(repeatRequest);
                var user = await userManager.FindByEmailAsync(request.AdministratorEmail);

                Assert.Equal(OrganizationProvisioningStatus.Created, firstResult.Status);
                Assert.Equal(OrganizationProvisioningStatus.AlreadyProvisioned, repeatResult.Status);
                Assert.Equal(originalPasswordHash, user?.PasswordHash);
                Assert.True(await userManager.CheckPasswordAsync(user!, originalPassword));
                Assert.False(await userManager.CheckPasswordAsync(user!, repeatPassword));
                Assert.Equal("PIT-CRM Teste", (await dbContext.Organizations.SingleAsync()).Name);
                Assert.Equal(1, await dbContext.Organizations.CountAsync());
                Assert.Equal(1, await dbContext.Users.CountAsync());
                Assert.Equal(1, await dbContext.Roles.CountAsync());
            }
        }
        finally
        {
            await using var connection = new NpgsqlConnection(adminConnection.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }
}
