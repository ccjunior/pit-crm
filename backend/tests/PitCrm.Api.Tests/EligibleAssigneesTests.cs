using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PitCrm.Application.Authorization;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class EligibleAssigneesTests
{
    [Fact]
    public async Task Every_role_sees_only_active_names_from_its_own_organization()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"assignees_{Guid.NewGuid():N}";
        var rootConnection = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = "public" };
        var scopedConnection = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schemaName };
        var previousConnectionString = Environment.GetEnvironmentVariable("PITCRM_CONNECTION_STRING");

        await using (var connection = new NpgsqlConnection(rootConnection.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            Environment.SetEnvironmentVariable("PITCRM_CONNECTION_STRING", scopedConnection.ConnectionString);
            using var factory = new WebApplicationFactory<Program>();
            using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var organizationId = Guid.NewGuid();
            var foreignOrganizationId = Guid.NewGuid();
            var password = $"Assignee-{Guid.NewGuid():N}-Dd4!";
            var accounts = new Dictionary<string, (string Email, string Name, Guid Id)>();

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync();
                dbContext.Organizations.AddRange(
                    new Organization(organizationId, "Organização A"),
                    new Organization(foreignOrganizationId, "Organização B"));
                await dbContext.SaveChangesAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
                {
                    Assert.True((await roleManager.CreateAsync(
                        new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() })).Succeeded);
                }

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
                {
                    var email = $"{roleName}-{organizationId:N}@example.invalid";
                    var user = new ApplicationUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = email,
                        Email = email,
                        DisplayName = roleName,
                        OrganizationId = organizationId,
                        IsActive = true
                    };
                    Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
                    Assert.True((await userManager.AddToRoleAsync(user, roleName)).Succeeded);
                    accounts.Add(roleName, (email, roleName, user.Id));
                }

                var inactiveEmail = $"inactive-{organizationId:N}@example.invalid";
                var inactive = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = inactiveEmail,
                    Email = inactiveEmail,
                    DisplayName = "Usuário Inativo",
                    OrganizationId = organizationId,
                    IsActive = false
                };
                Assert.True((await userManager.CreateAsync(inactive, password)).Succeeded);
                Assert.True((await userManager.AddToRoleAsync(inactive, RoleNames.Operator)).Succeeded);

                var foreignEmail = $"foreign-{foreignOrganizationId:N}@example.invalid";
                var foreign = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = foreignEmail,
                    Email = foreignEmail,
                    DisplayName = "Usuário de Outra Organização",
                    OrganizationId = foreignOrganizationId,
                    IsActive = true
                };
                Assert.True((await userManager.CreateAsync(foreign, password)).Succeeded);
                Assert.True((await userManager.AddToRoleAsync(foreign, RoleNames.Operator)).Succeeded);
            }

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymousClient.GetAsync("/api/users/eligible-assignees")).StatusCode);

            var expected = accounts.Values
                .Select(account => new AssigneeResponse(account.Id, account.Name))
                .OrderBy(account => account.DisplayName, StringComparer.Ordinal)
                .ToArray();

            foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
            {
                using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });
                var csrf = await GetCsrfTokenAsync(client);
                using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
                {
                    Content = JsonContent.Create(new { email = accounts[roleName].Email, password })
                };
                login.Headers.Add("X-CSRF-TOKEN", csrf);
                Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);

                var response = await client.GetAsync("/api/users/eligible-assignees");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("role", json, StringComparison.OrdinalIgnoreCase);
                var results = JsonSerializer.Deserialize<AssigneeResponse[]>(json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                Assert.Equal(expected, results);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PITCRM_CONNECTION_STRING", previousConnectionString);
            await using var connection = new NpgsqlConnection(rootConnection.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        Assert.NotNull(response?.RequestToken);
        return response.RequestToken;
    }

    private sealed record CsrfResponse(string RequestToken);
    private sealed record AssigneeResponse(Guid Id, string DisplayName);
}
