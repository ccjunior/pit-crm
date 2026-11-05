using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PitCrm.Api.Authorization;
using PitCrm.Api.Features.Customers;
using PitCrm.Api.Features.Locations;
using PitCrm.Api.Features.Demands;
using PitCrm.Api.Features.Users;
using PitCrm.Application.Authorization;
using PitCrm.Application.Organizations.Provisioning;
using PitCrm.Application.Users.EligibleAssignees;
using PitCrm.Application.Customers;
using PitCrm.Application.Locations;
using PitCrm.Application.Demands;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;
using PitCrm.Infrastructure.Provisioning;
using PitCrm.Infrastructure.Users.EligibleAssignees;
using PitCrm.Infrastructure.Customers;
using PitCrm.Infrastructure.Locations;
using PitCrm.Infrastructure.Demands;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["PITCRM_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure PITCRM_CONNECTION_STRING before starting PIT-CRM.");
}

builder.Services.AddDbContext<PitCrmDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<PitCrmDbContext>()
    .AddSignInManager();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, OrganizationClaimsPrincipalFactory>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<PitCrmDbContext>();
        var account = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.IsActive,
                user.MustChangePassword,
                user.OrganizationId,
                OrganizationIsActive = user.Organization.IsActive,
                Roles = dbContext.UserRoles.Where(role => role.UserId == user.Id)
                    .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
                    .ToArray()
            })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        var organizationClaim = context.Principal?.FindFirstValue(OrganizationIdClaim.Type);
        var passwordChangeClaim = context.Principal?.FindFirstValue(AuthorizationPolicies.MustChangePasswordClaim);
        var sessionRoles = context.Principal?.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray() ?? [];
        var currentRoles = account?.Roles
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray() ?? [];
        if (account is not { IsActive: true, OrganizationIsActive: true }
            || !Guid.TryParse(organizationClaim, out var organizationId)
            || organizationId != account.OrganizationId
            || !string.Equals(passwordChangeClaim, account.MustChangePassword ? "true" : "false", StringComparison.Ordinal)
            || !sessionRoles.SequenceEqual(currentRoles, StringComparer.Ordinal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ReadCustomersAndLocations, policy =>
        policy.RequireRole(RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator)
            .RequireClaim(AuthorizationPolicies.MustChangePasswordClaim, "false"));
    options.AddPolicy(AuthorizationPolicies.ManageCustomersAndLocations, policy =>
        policy.RequireRole(RoleNames.Administrator, RoleNames.Manager)
            .RequireClaim(AuthorizationPolicies.MustChangePasswordClaim, "false"));
    options.AddPolicy(AuthorizationPolicies.ManageTeam, policy =>
        policy.RequireRole(RoleNames.Administrator)
            .RequireClaim(AuthorizationPolicies.MustChangePasswordClaim, "false"));
    options.AddPolicy(AuthorizationPolicies.OperateDemands, policy =>
        policy.RequireRole(RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator)
            .RequireClaim(AuthorizationPolicies.MustChangePasswordClaim, "false"));
    options.AddPolicy(AuthorizationPolicies.ChangeOwnPassword, policy =>
        policy.RequireRole(RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator));
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddScoped<IOrganizationProvisioner, OrganizationProvisioner>();
builder.Services.AddScoped<IEligibleAssigneeQuery, EligibleAssigneeQuery>();
builder.Services.AddScoped<ICustomerStore, PersistenceCustomerStore>();
builder.Services.AddScoped<ICustomerLocationStore, PersistenceCustomerLocationStore>();
builder.Services.AddScoped<IDemandStore, PersistenceDemandStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentOrganizationContext, HttpCurrentOrganizationContext>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (args.FirstOrDefault() == "provision")
{
    using var scope = app.Services.CreateScope();
    var request = new OrganizationProvisioningRequest
    {
        OrganizationId = Guid.Parse(RequiredEnvironmentValue("PITCRM_SETUP_ORGANIZATION_ID")),
        OrganizationName = RequiredEnvironmentValue("PITCRM_SETUP_ORGANIZATION_NAME"),
        AdministratorDisplayName = RequiredEnvironmentValue("PITCRM_SETUP_ADMIN_NAME"),
        AdministratorEmail = RequiredEnvironmentValue("PITCRM_SETUP_ADMIN_EMAIL"),
        AdministratorPassword = RequiredEnvironmentValue("PITCRM_SETUP_ADMIN_PASSWORD")
    };

    var provisioner = scope.ServiceProvider.GetRequiredService<IOrganizationProvisioner>();
    var result = await provisioner.ProvisionAsync(request);
    var message = result.Status == OrganizationProvisioningStatus.Created
        ? "Provisioning completed."
        : "Organization was already provisioned; no account or credential was changed.";
    Console.WriteLine($"{message} OrganizationId={result.OrganizationId}");
    return;
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    HttpContext context,
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    PitCrmDbContext dbContext) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (!await IsAntiforgeryValidAsync(context, antiforgery))
    {
        return Results.BadRequest(new { error = "Proteção da solicitação inválida. Atualize a página e tente novamente." });
    }

    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
    {
        return Results.BadRequest(new { error = "Informe e-mail e senha." });
    }

    var user = await userManager.FindByEmailAsync(request.Email.Trim());
    var eligible = user is { IsActive: true } && await dbContext.Organizations.AsNoTracking()
        .AnyAsync(organization => organization.Id == user.OrganizationId && organization.IsActive);
    var result = eligible
        ? await signInManager.PasswordSignInAsync(user!, request.Password, isPersistent: false, lockoutOnFailure: true)
        : SignInResult.Failed;

    if (!result.Succeeded)
    {
        return Results.Json(new { error = "E-mail ou senha inválidos." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new { authenticated = true });
}).AllowAnonymous();

app.MapGet("/api/auth/me", async (HttpContext context, ClaimsPrincipal principal, PitCrmDbContext dbContext) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdValue, out var userId))
    {
        return Results.Unauthorized();
    }

    var session = await dbContext.Users.AsNoTracking()
        .Where(user => user.Id == userId && user.IsActive && user.Organization.IsActive)
        .Select(user => new
        {
            user.Id,
            user.DisplayName,
            user.MustChangePassword,
            Email = user.Email!,
            Organization = new { user.Organization.Id, user.Organization.Name },
            Roles = dbContext.UserRoles.Where(role => role.UserId == user.Id)
                .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name)
                .ToArray()
        })
        .SingleOrDefaultAsync();

    return session is null ? Results.Unauthorized() : Results.Ok(session);
}).RequireAuthorization();

app.MapGet("/api/users/eligible-assignees", async (
    ICurrentOrganizationContext organizationContext,
    IEligibleAssigneeQuery query,
    CancellationToken cancellationToken) =>
{
    var assignees = await query.ExecuteAsync(organizationContext.OrganizationId, cancellationToken);
    return Results.Ok(assignees);
}).RequireAuthorization(AuthorizationPolicies.OperateDemands);

app.MapPost("/api/auth/logout", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager) =>
{
    if (!await IsAntiforgeryValidAsync(context, antiforgery))
    {
        return Results.BadRequest(new { error = "Proteção da solicitação inválida. Atualize a página e tente novamente." });
    }

    await signInManager.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/api/auth/change-password", async (
    ChangePasswordRequest request,
    HttpContext context,
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ICurrentOrganizationContext organizationContext) =>
{
    if (!await IsAntiforgeryValidAsync(context, antiforgery))
    {
        return Results.BadRequest(new { error = "Proteção da solicitação inválida. Atualize a página e tente novamente." });
    }
    if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["NewPassword"] = ["Informe a senha atual e uma nova senha."] });
    }

    var user = await userManager.FindByIdAsync(organizationContext.UserId.ToString());
    if (user is null || !user.IsActive || user.OrganizationId != organizationContext.OrganizationId) return Results.Unauthorized();
    var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded)
    {
        var errors = result.Errors.Any(error => error.Code == "PasswordMismatch")
            ? new Dictionary<string, string[]> { ["CurrentPassword"] = ["A senha atual está incorreta."] }
            : new Dictionary<string, string[]> { ["NewPassword"] = ["A nova senha não atende aos requisitos de segurança."] };
        return Results.ValidationProblem(errors);
    }

    user.MustChangePassword = false;
    var updateResult = await userManager.UpdateAsync(user);
    if (!updateResult.Succeeded) return Results.Problem("Não foi possível atualizar a credencial.");
    await signInManager.RefreshSignInAsync(user);
    return Results.NoContent();
}).RequireAuthorization(AuthorizationPolicies.ChangeOwnPassword);

CustomerEndpoints.Map(app);
CustomerLocationEndpoints.Map(app);
DemandEndpoints.Map(app);
TeamEndpoints.Map(app);

static string RequiredEnvironmentValue(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Set the required environment variable {name}.");
    }

    return value;
}

static async Task<bool> IsAntiforgeryValidAsync(HttpContext context, IAntiforgery antiforgery)
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}

app.Run();

public partial class Program { }
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
