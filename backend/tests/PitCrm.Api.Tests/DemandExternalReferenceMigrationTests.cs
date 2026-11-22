using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PitCrm.Application.Organizations.Provisioning;
using PitCrm.Domain.Customers;
using PitCrm.Domain.Locations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;
using PitCrm.Infrastructure.Provisioning;

namespace PitCrm.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class DemandExternalReferenceMigrationTests
{
    [Fact]
    public async Task Migration_preserves_legacy_ticket_and_purchase_order_values_as_external_references()
    {
        var connectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"demand_refs_{Guid.NewGuid():N}";
        var originalConnection = new NpgsqlConnectionStringBuilder(connectionString);
        var adminConnection = new NpgsqlConnectionStringBuilder(originalConnection.ConnectionString) { SearchPath = "public" };
        var isolatedConnection = new NpgsqlConnectionStringBuilder(originalConnection.ConnectionString) { SearchPath = schemaName };

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
            services.AddDbContext<PitCrmDbContext>(options => options.UseNpgsql(isolatedConnection.ConnectionString));
            services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<PitCrmDbContext>();
            services.AddScoped<IOrganizationProvisioner, OrganizationProvisioner>();

            using var provider = services.BuildServiceProvider();
            var organizationId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var demandId = Guid.NewGuid();
            Guid administratorId;
            var now = DateTimeOffset.UtcNow;

            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync("20261120144449_AddDemandVersion");

                var provisioner = scope.ServiceProvider.GetRequiredService<IOrganizationProvisioner>();
                await provisioner.ProvisionAsync(new OrganizationProvisioningRequest
                {
                    OrganizationId = organizationId,
                    OrganizationName = "Empresa de migração de referências",
                    AdministratorDisplayName = "Admin de teste",
                    AdministratorEmail = $"references-{organizationId:N}@example.invalid",
                    AdministratorPassword = $"Test-{Guid.NewGuid():N}-Aa1!"
                });
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var administrator = await userManager.FindByEmailAsync($"references-{organizationId:N}@example.invalid");
                Assert.NotNull(administrator);
                administratorId = administrator.Id;

                dbContext.Customers.Add(new Customer(customerId, organizationId, administratorId, "Cliente legado",
                    null, null, null, null, null, null, null, now));
                dbContext.CustomerLocations.Add(new CustomerLocation(locationId, organizationId, customerId, administratorId,
                    "Unidade legado", "Rua antiga, 1", null, null, null, null, now));
                await dbContext.SaveChangesAsync();

                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Demands" ("Id", "OrganizationId", "CustomerId", "LocationId", "ReferenceNumber",
                        "Description", "Origin", "Priority", "Status", "TicketReference", "PurchaseOrderReference",
                        "AssignedToUserId", "CreatedByUserId", "CreatedAtUtc", "Version")
                    VALUES ({demandId}, {organizationId}, {customerId}, {locationId}, 1,
                        'Demanda legada', 'Outra', NULL, 'Recebida', '  CH-LEGACY-1  ', 'OC-LEGACY-9',
                        NULL, {administratorId}, {now}, 1)
                    """);

                await dbContext.Database.MigrateAsync();

                var references = await dbContext.DemandExternalReferences.AsNoTracking()
                    .Where(reference => reference.OrganizationId == organizationId && reference.DemandId == demandId)
                    .OrderBy(reference => reference.Type)
                    .Select(reference => new { reference.Type, reference.Value })
                    .ToListAsync();
                Assert.Collection(references,
                    reference =>
                    {
                        Assert.Equal("Chamado/OS", reference.Type);
                        Assert.Equal("CH-LEGACY-1", reference.Value);
                    },
                    reference =>
                    {
                        Assert.Equal("Ordem de Compra", reference.Type);
                        Assert.Equal("OC-LEGACY-9", reference.Value);
                    });
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
