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
public sealed class TeamAndPasswordApiTests
{
    [Fact]
    public async Task Administrator_creates_team_member_with_forced_password_change()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"team_{Guid.NewGuid():N}";
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
            var organizationA = Guid.NewGuid();
            var organizationB = Guid.NewGuid();
            var password = $"Team-{Guid.NewGuid():N}-Ee5!";
            var users = new Dictionary<string, string>();
            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync();
                dbContext.Organizations.AddRange(new Organization(organizationA, "Empresa A"), new Organization(organizationB, "Empresa B"));
                await dbContext.SaveChangesAsync();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                foreach (var role in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
                    Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() })).Succeeded);
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                await AddUserAsync(userManager, users, "A", organizationA, RoleNames.Administrator, password);
                await AddUserAsync(userManager, users, "A", organizationA, RoleNames.Manager, password);
                await AddUserAsync(userManager, users, "A", organizationA, RoleNames.Operator, password);
                await AddUserAsync(userManager, users, "B", organizationB, RoleNames.Administrator, password);
            }

            using var adminClient = await CreateAuthenticatedClientAsync(factory, users["A-" + RoleNames.Administrator], password);
            using var managerClient = await CreateAuthenticatedClientAsync(factory, users["A-" + RoleNames.Manager], password);
            using var operatorClient = await CreateAuthenticatedClientAsync(factory, users["A-" + RoleNames.Operator], password);
            using var foreignClient = await CreateAuthenticatedClientAsync(factory, users["B-" + RoleNames.Administrator], password);
            using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/api/users")).StatusCode);

            var invalidRole = await PostJsonAsync(adminClient, "/api/users", new
            {
                displayName = "Pessoa Teste", email = "person@example.invalid", role = "Financeiro", temporaryPassword = "Temporary-123!"
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);

            const string temporaryPassword = "Initial-Team-123!";
            const string email = "new-member@example.invalid";
            var createdResponse = await PostJsonAsync(adminClient, "/api/users", new
            {
                displayName = "Nova Operadora", email, role = "Operador", temporaryPassword
            });
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var createdJson = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync());
            var createdRoot = createdJson.RootElement;
            var createdId = createdRoot.GetProperty("id").GetGuid();
            Assert.Equal("Nova Operadora", createdRoot.GetProperty("displayName").GetString());
            Assert.Equal("Operador", createdRoot.GetProperty("role").GetString());
            Assert.True(createdRoot.GetProperty("mustChangePassword").GetBoolean());
            Assert.False(createdRoot.TryGetProperty("temporaryPassword", out _));
            Assert.False(createdRoot.TryGetProperty("passwordHash", out _));

            var list = await adminClient.GetFromJsonAsync<TeamPageResponse>("/api/users?page=1&pageSize=20");
            Assert.NotNull(list);
            Assert.Equal(4, list.TotalCount);
            var listedMember = Assert.Single(list.Items, item => item.Id == createdId);
            Assert.True(listedMember.MustChangePassword);
            var foreignList = await foreignClient.GetFromJsonAsync<TeamPageResponse>("/api/users");
            Assert.NotNull(foreignList);
            Assert.Equal(1, foreignList.TotalCount);
            Assert.DoesNotContain(foreignList.Items, item => item.Id == createdId);

            using var newMemberClient = await CreateAuthenticatedClientAsync(factory, email, temporaryPassword);
            using var protectedResponse = await newMemberClient.GetAsync("/api/customers");
            Assert.Equal(HttpStatusCode.Forbidden, protectedResponse.StatusCode);
            var me = await newMemberClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
            Assert.NotNull(me);
            Assert.True(me.MustChangePassword);

            var wrongCurrentPassword = await PostJsonAsync(newMemberClient, "/api/auth/change-password", new
            {
                currentPassword = "wrong-password", newPassword = "Changed-Team-456!"
            });
            Assert.Equal(HttpStatusCode.BadRequest, wrongCurrentPassword.StatusCode);

            const string newPassword = "Changed-Team-456!";
            var changed = await PostJsonAsync(newMemberClient, "/api/auth/change-password", new
            {
                currentPassword = temporaryPassword, newPassword
            });
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            me = await newMemberClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
            Assert.NotNull(me);
            Assert.False(me.MustChangePassword);
            Assert.Equal(HttpStatusCode.OK, (await newMemberClient.GetAsync("/api/customers")).StatusCode);

            using var oldPasswordClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var oldPasswordLogin = await LoginAsync(oldPasswordClient, email, temporaryPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
            using var newPasswordClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var newPasswordLogin = await LoginAsync(newPasswordClient, email, newPassword);
            Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);

            var backupAdminEmail = "backup-admin@example.invalid";
            var backupAdminResponse = await PostJsonAsync(adminClient, "/api/users", new
            {
                displayName = "Administradora Reserva", email = backupAdminEmail, role = "Administrador", temporaryPassword = "Backup-Team-789!"
            });
            Assert.Equal(HttpStatusCode.Created, backupAdminResponse.StatusCode);
            var backupAdminId = (await backupAdminResponse.Content.ReadFromJsonAsync<TeamMemberResponse>())!.Id;
            using var backupAdminClient = await CreateAuthenticatedClientAsync(factory, backupAdminEmail, "Backup-Team-789!");
            const string backupAdminPassword = "Backup-Changed-987!";
            var backupPasswordChanged = await PostJsonAsync(backupAdminClient, "/api/auth/change-password", new
            {
                currentPassword = "Backup-Team-789!", newPassword = backupAdminPassword
            });
            Assert.Equal(HttpStatusCode.NoContent, backupPasswordChanged.StatusCode);

            var managerUpdate = await PutJsonAsync(managerClient, $"/api/users/{createdId}", new
            {
                displayName = "Operadora Atualizada", email = email, role = "Operador", isActive = true
            });
            Assert.Equal(HttpStatusCode.Forbidden, managerUpdate.StatusCode);
            var foreignUpdate = await PutJsonAsync(foreignClient, $"/api/users/{createdId}", new
            {
                displayName = "Alteração Externa", email, role = "Operador", isActive = true
            });
            Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
            var edited = await PutJsonAsync(adminClient, $"/api/users/{createdId}", new
            {
                displayName = "Operadora Atualizada", email = "updated-member@example.invalid", role = "Operador", isActive = true
            });
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

            Guid originalAdminId;
            using (var scope = factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                originalAdminId = (await userManager.FindByEmailAsync(users["A-" + RoleNames.Administrator]))!.Id;
            }

            var concurrentUpdates = await Task.WhenAll(
                PutJsonAsync(adminClient, $"/api/users/{originalAdminId}", new
                {
                    displayName = "Administração", email = users["A-" + RoleNames.Administrator], role = "Gestor", isActive = true
                }),
                PutJsonAsync(backupAdminClient, $"/api/users/{backupAdminId}", new
                {
                    displayName = "Administradora Reserva", email = backupAdminEmail, role = "Gestor", isActive = true
                }));
            Assert.Single(concurrentUpdates, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Contains(concurrentUpdates, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden);

            string remainingAdminEmail;
            Guid remainingAdminId;
            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var remainingAdmin = await dbContext.Users
                    .Where(value => value.OrganizationId == organizationA && value.IsActive)
                    .Join(dbContext.UserRoles, value => value.Id, userRole => userRole.UserId, (value, userRole) => new { value, userRole.RoleId })
                    .Join(dbContext.Roles.Where(value => value.Name == RoleNames.Administrator), userRole => userRole.RoleId,
                        role => role.Id, (userRole, _) => userRole.value)
                    .SingleAsync();
                remainingAdminEmail = remainingAdmin.Email!;
                remainingAdminId = remainingAdmin.Id;
            }
            var remainingAdminPassword = remainingAdminEmail == backupAdminEmail ? backupAdminPassword : password;
            using var remainingAdminClient = await CreateAuthenticatedClientAsync(factory, remainingAdminEmail, remainingAdminPassword);
            var deactivateLastAdmin = await PutJsonAsync(remainingAdminClient, $"/api/users/{remainingAdminId}", new
            {
                displayName = "Último Administrador", email = remainingAdminEmail, role = "Administrador", isActive = false
            });
            Assert.Equal(HttpStatusCode.Conflict, deactivateLastAdmin.StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var user = await dbContext.Users.SingleAsync(value => value.Id == createdId);
                Assert.Equal(organizationA, user.OrganizationId);
                Assert.False(user.MustChangePassword);
                Assert.NotNull(user.PasswordHash);
                var activeAdministrators = await dbContext.Users
                    .Where(value => value.OrganizationId == organizationA && value.IsActive)
                    .Join(dbContext.UserRoles, value => value.Id, userRole => userRole.UserId, (_, userRole) => userRole)
                    .Join(dbContext.Roles.Where(value => value.Name == RoleNames.Administrator), userRole => userRole.RoleId,
                        role => role.Id, (_, _) => 1)
                    .CountAsync();
                Assert.Equal(1, activeAdministrators);
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

    private static async Task AddUserAsync(UserManager<ApplicationUser> userManager, IDictionary<string, string> users,
        string organizationKey, Guid organizationId, string role, string password)
    {
        var email = $"{role}-{organizationId:N}@example.invalid";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = role,
            OrganizationId = organizationId, IsActive = true
        };
        Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
        users[$"{organizationKey}-{role}"] = email;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var response = await LoginAsync(client, email, password);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            client.Dispose();
            throw new InvalidOperationException("Test user could not authenticate.");
        }
        return client;
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.RequestToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.RequestToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PutJsonAsync(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        Assert.NotNull(csrf);
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.RequestToken);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);
    private sealed record TeamMemberResponse(Guid Id, string DisplayName, string Email, string Role, bool IsActive, bool MustChangePassword);
    private sealed record TeamPageResponse(TeamMemberResponse[] Items, int Page, int PageSize, int TotalCount);
    private sealed record SessionResponse(Guid Id, string DisplayName, string Email, bool MustChangePassword);
}
