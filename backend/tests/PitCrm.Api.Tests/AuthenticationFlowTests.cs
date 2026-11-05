using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using PitCrm.Application.Authorization;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class AuthenticationFlowTests
{
    [Fact]
    public async Task Session_requires_csrf_and_rejects_inactive_accounts_and_organizations()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"auth_{Guid.NewGuid():N}";
        var rootConnection = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = "public"
        };
        var scopedConnection = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schemaName
        };
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
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var services = factory.Services;
            Guid organizationId;
            Guid userId;
            var email = $"auth-{Guid.NewGuid():N}@example.invalid";
            var password = $"Session-{Guid.NewGuid():N}-Cc3!";

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync();

                organizationId = Guid.NewGuid();
                dbContext.Organizations.Add(new Organization(organizationId, "Organização de Teste"));
                await dbContext.SaveChangesAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
                {
                    var role = new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() };
                    Assert.True((await roleManager.CreateAsync(role)).Succeeded);
                }

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    DisplayName = "Usuário de Teste",
                    OrganizationId = organizationId,
                    IsActive = true
                };
                Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
                Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Administrator)).Succeeded);
                userId = user.Id;

                foreach (var roleName in new[] { RoleNames.Manager, RoleNames.Operator })
                {
                    var teammateEmail = $"{roleName}-{organizationId:N}@example.invalid";
                    var teammate = new ApplicationUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = teammateEmail,
                        Email = teammateEmail,
                        DisplayName = roleName,
                        OrganizationId = organizationId,
                        IsActive = true
                    };
                    Assert.True((await userManager.CreateAsync(teammate, password)).Succeeded);
                    Assert.True((await userManager.AddToRoleAsync(teammate, roleName)).Succeeded);
                }
            }

            using (var scope = services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
                var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
                var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
                var organizationContext = scope.ServiceProvider
                    .GetRequiredService<PitCrm.Application.Authorization.ICurrentOrganizationContext>();

                foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
                {
                    var testEmail = roleName == RoleNames.Administrator
                        ? email
                        : $"{roleName}-{organizationId:N}@example.invalid";
                    var user = await userManager.FindByEmailAsync(testEmail);
                    Assert.NotNull(user);
                    var principal = await signInManager.CreateUserPrincipalAsync(user);
                    httpContextAccessor.HttpContext = new DefaultHttpContext { User = principal };
                    Assert.Equal(user.Id, organizationContext.UserId);
                    Assert.Equal(organizationId, organizationContext.OrganizationId);

                    var canRead = await authorization.AuthorizeAsync(principal,
                        AuthorizationPolicies.ReadCustomersAndLocations);
                    var canManageRecords = await authorization.AuthorizeAsync(principal,
                        AuthorizationPolicies.ManageCustomersAndLocations);
                    var canManageTeam = await authorization.AuthorizeAsync(principal,
                        AuthorizationPolicies.ManageTeam);
                    var canOperateDemands = await authorization.AuthorizeAsync(principal,
                        AuthorizationPolicies.OperateDemands);
                    var canChangeOwnPassword = await authorization.AuthorizeAsync(principal,
                        AuthorizationPolicies.ChangeOwnPassword);

                    Assert.True(canRead.Succeeded);
                    Assert.Equal(roleName != RoleNames.Operator, canManageRecords.Succeeded);
                    Assert.Equal(roleName == RoleNames.Administrator, canManageTeam.Succeeded);
                    Assert.True(canOperateDemands.Succeeded);
                    Assert.True(canChangeOwnPassword.Succeeded);
                }
                httpContextAccessor.HttpContext = null;
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
            var cookieOptions = factory.Services
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);
            var unauthenticatedContext = new DefaultHttpContext();
            await cookieOptions.Events.OnRedirectToLogin(new RedirectContext<CookieAuthenticationOptions>(
                unauthenticatedContext,
                new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
                cookieOptions,
                new AuthenticationProperties(),
                "/api/auth/me"));
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthenticatedContext.Response.StatusCode);
            var forbiddenContext = new DefaultHttpContext();
            await cookieOptions.Events.OnRedirectToAccessDenied(new RedirectContext<CookieAuthenticationOptions>(
                forbiddenContext,
                new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
                cookieOptions,
                new AuthenticationProperties(),
                "/api/auth/me"));
            Assert.Equal(StatusCodes.Status403Forbidden, forbiddenContext.Response.StatusCode);

            var csrf = await GetCsrfTokenAsync(client);
            var invalidLogin = await PostLoginAsync(client, csrf, email, "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);
            Assert.Equal("E-mail ou senha inválidos.",
                (await invalidLogin.Content.ReadFromJsonAsync<ErrorResponse>())?.Error);

            csrf = await GetCsrfTokenAsync(client);
            var login = await PostLoginAsync(client, csrf, email, password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var authCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
            Assert.Contains("httponly", authCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", authCookie, StringComparison.OrdinalIgnoreCase);

            var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/me");
            Assert.NotNull(session);
            Assert.Equal(userId, session.Id);
            Assert.Equal("Usuário de Teste", session.DisplayName);
            Assert.Equal(organizationId, session.Organization.Id);
            Assert.Equal("Organização de Teste", session.Organization.Name);
            Assert.Contains(RoleNames.Administrator, session.Roles);

            var logoutWithoutCsrf = await client.PostAsync("/api/auth/logout", content: null);
            Assert.Equal(HttpStatusCode.BadRequest, logoutWithoutCsrf.StatusCode);

            csrf = await GetCsrfTokenAsync(client);
            using (var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
            {
                logoutRequest.Headers.Add("X-CSRF-TOKEN", csrf);
                Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logoutRequest)).StatusCode);
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

            csrf = await GetCsrfTokenAsync(client);
            Assert.Equal(HttpStatusCode.OK, (await PostLoginAsync(client, csrf, email, password)).StatusCode);
            using (var scope = services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(user);
                Assert.True((await userManager.RemoveFromRoleAsync(user, RoleNames.Administrator)).Succeeded);
                Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Manager)).Succeeded);
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

            csrf = await GetCsrfTokenAsync(client);
            Assert.Equal(HttpStatusCode.OK, (await PostLoginAsync(client, csrf, email, password)).StatusCode);
            var updatedSession = await client.GetFromJsonAsync<SessionResponse>("/api/auth/me");
            Assert.NotNull(updatedSession);
            Assert.Contains(RoleNames.Manager, updatedSession.Roles);
            Assert.DoesNotContain(RoleNames.Administrator, updatedSession.Roles);

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
                user.IsActive = false;
                await dbContext.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
                user.IsActive = true;
                var organization = await dbContext.Organizations.SingleAsync(item => item.Id == organizationId);
                organization.Deactivate();
                await dbContext.SaveChangesAsync();
            }

            csrf = await GetCsrfTokenAsync(client);
            var inactiveOrganizationLogin = await PostLoginAsync(client, csrf, email, password);
            Assert.Equal(HttpStatusCode.Unauthorized, inactiveOrganizationLogin.StatusCode);
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

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string csrfToken, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);
    private sealed record ErrorResponse(string Error);
    private sealed record SessionResponse(Guid Id, string DisplayName, string Email, OrganizationSession Organization, string[] Roles);
    private sealed record OrganizationSession(Guid Id, string Name);
}
