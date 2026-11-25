using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PitCrm.Domain.Customers;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class CommercialTrackingMigrationTests
{
    [Fact]
    public async Task AddCommercialTrackingFields_backfills_deterministic_numbers_without_changing_legacy_identity_or_foreign_keys()
    {
        var connectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"commercial_tracking_{Guid.NewGuid():N}";
        var rootConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = "public" };
        var isolatedConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schemaName };
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var administratorAId = Guid.NewGuid();
        var administratorBId = Guid.NewGuid();
        var legacyCustomerId = Guid.NewGuid();
        var earlyId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tieLowerId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var tieHigherId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var otherOrganizationId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var nextYearId = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var earlyCreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tiedCreatedAt = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var nextYearCreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await using (var connection = new NpgsqlConnection(rootConnection.ConnectionString))
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

            using var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync("20261122005302_AddCommercialProposalsAndDemandDrafts");
                dbContext.Organizations.AddRange(
                    new Organization(organizationA, "Organização legada A"),
                    new Organization(organizationB, "Organização legada B"));
                await dbContext.SaveChangesAsync();

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                await CreateUserAsync(userManager, organizationA, administratorAId);
                await CreateUserAsync(userManager, organizationB, administratorBId);
                dbContext.Customers.Add(new Customer(legacyCustomerId, organizationA, administratorAId,
                    "Cliente legado", null, null, null, null, null, null, null, earlyCreatedAt));
                await dbContext.SaveChangesAsync();

                await InsertLegacyRequestAsync(dbContext, earlyId, organizationA, administratorAId,
                    "Legado anterior", "E-mail", earlyCreatedAt, legacyCustomerId);
                await InsertLegacyRequestAsync(dbContext, tieHigherId, organizationA, administratorAId,
                    "Empate ID maior", "WhatsApp", tiedCreatedAt);
                await InsertLegacyRequestAsync(dbContext, tieLowerId, organizationA, administratorAId,
                    "Empate ID menor", "Ligação", tiedCreatedAt);
                await InsertLegacyRequestAsync(dbContext, nextYearId, organizationA, administratorAId,
                    "Ano seguinte", "E-mail", nextYearCreatedAt);
                await InsertLegacyRequestAsync(dbContext, otherOrganizationId, organizationB, administratorBId,
                    "Outra organização", "Indicação", earlyCreatedAt);

                await dbContext.Database.MigrateAsync();

                var rowsA = await dbContext.CommercialRequests.AsNoTracking()
                    .Where(value => value.OrganizationId == organizationA)
                    .OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id)
                    .ToListAsync();
                Assert.Collection(rowsA,
                    value =>
                    {
                        Assert.Equal(earlyId, value.Id);
                        Assert.Equal(administratorAId, value.CreatedByUserId);
                        Assert.Equal(legacyCustomerId, value.CustomerId);
                        Assert.Equal("SOL-2024-0001", value.Number);
                        Assert.Equal("E-mail", value.Origin);
                    },
                    value =>
                    {
                        Assert.Equal(tieLowerId, value.Id);
                        Assert.Equal(administratorAId, value.CreatedByUserId);
                        Assert.Equal("SOL-2024-0002", value.Number);
                        Assert.Equal("Telefone", value.Origin);
                    },
                    value =>
                    {
                        Assert.Equal(tieHigherId, value.Id);
                        Assert.Equal(administratorAId, value.CreatedByUserId);
                        Assert.Equal("SOL-2024-0003", value.Number);
                        Assert.Equal("WhatsApp", value.Origin);
                    },
                    value =>
                    {
                        Assert.Equal(nextYearId, value.Id);
                        Assert.Equal(administratorAId, value.CreatedByUserId);
                        Assert.Equal("SOL-2025-0001", value.Number);
                        Assert.Equal("E-mail", value.Origin);
                    });
                Assert.All(rowsA, value => Assert.Equal("Manual", value.SourceType));

                var rowB = await dbContext.CommercialRequests.AsNoTracking()
                    .SingleAsync(value => value.OrganizationId == organizationB);
                Assert.Equal(otherOrganizationId, rowB.Id);
                Assert.Equal(administratorBId, rowB.CreatedByUserId);
                Assert.Equal("SOL-2024-0001", rowB.Number);
                Assert.Equal(5, await dbContext.CommercialRequests.CountAsync());
            }
        }
        finally
        {
            await using var connection = new NpgsqlConnection(rootConnection.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateUserAsync(UserManager<ApplicationUser> userManager,
        Guid organizationId, Guid userId)
    {
        var email = $"legacy-{userId:N}@example.invalid";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            DisplayName = "Administrador de fixture",
            OrganizationId = organizationId,
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, $"Legacy-{Guid.NewGuid():N}-Aa1!");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(value => value.Code)));
    }

    private static Task<int> InsertLegacyRequestAsync(PitCrmDbContext dbContext, Guid id,
        Guid organizationId, Guid createdByUserId, string prospectName, string origin,
        DateTimeOffset createdAtUtc, Guid? customerId = null) => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CommercialRequests" (
                "Id", "OrganizationId", "CustomerId", "ProspectName", "Origin", "Description",
                "Status", "OutcomeReason", "CreatedByUserId", "Version", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES (
                {id}, {organizationId}, {customerId}, {prospectName}, {origin}, 'Solicitação legada de teste',
                'Received', NULL, {createdByUserId}, 1, {createdAtUtc}, {createdAtUtc})
            """);
}
