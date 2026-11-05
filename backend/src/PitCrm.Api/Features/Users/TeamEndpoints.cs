using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Authorization;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Features.Users;

public static class TeamEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/users", ListAsync).RequireAuthorization(AuthorizationPolicies.ManageTeam);
        app.MapPost("/api/users", CreateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageTeam);
        app.MapPut("/api/users/{userId:guid}", UpdateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageTeam);
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, ICurrentOrganizationContext context,
        PitCrmDbContext dbContext, CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1 || (long)(currentPage - 1) * currentPageSize > int.MaxValue)
            errors["page"] = ["A página deve ser um número positivo dentro do limite permitido."];
        if (currentPageSize < 1 || currentPageSize > 100)
            errors["pageSize"] = ["O tamanho da página deve estar entre 1 e 100."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var users = dbContext.Users.AsNoTracking().Where(user => user.OrganizationId == context.OrganizationId);
        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((currentPage - 1) * currentPageSize).Take(currentPageSize)
            .Select(user => new TeamMemberDto(user.Id, user.DisplayName, user.Email!,
                dbContext.UserRoles.Where(userRole => userRole.UserId == user.Id)
                    .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
                    .FirstOrDefault() ?? string.Empty,
                user.IsActive, user.MustChangePassword))
            .ToListAsync(cancellationToken);
        return Results.Ok(new TeamPage(items.ToArray(), currentPage, currentPageSize, totalCount));
    }

    private static async Task<IResult> CreateAsync(CreateTeamMemberRequest request,
        ICurrentOrganizationContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        PitCrmDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var role = new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator }
            .Single(value => string.Equals(value, request.Role!.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!await roleManager.RoleExistsAsync(role))
            return Results.Problem("O perfil solicitado não está configurado.", statusCode: StatusCodes.Status500InternalServerError);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var email = request.Email!.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName!.Trim(),
            OrganizationId = context.OrganizationId,
            IsActive = true,
            MustChangePassword = true
        };
        var createResult = await userManager.CreateAsync(user, request.TemporaryPassword!);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(error => error.Code is "DuplicateUserName" or "DuplicateEmail"))
                return Results.Conflict(new { error = "Não foi possível cadastrar essa conta. Confira os dados ou fale com o Administrador." });
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["TemporaryPassword"] = ["A senha temporária não atende aos requisitos de segurança. Use uma senha forte."]
            });
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            return Results.Problem("Não foi possível concluir o cadastro da conta.", statusCode: StatusCodes.Status500InternalServerError);

        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/users/{user.Id}", new TeamMemberDto(
            user.Id, user.DisplayName, user.Email!, role, user.IsActive, user.MustChangePassword));
    }

    private static Dictionary<string, string[]> Validate(CreateTeamMemberRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.DisplayName)) errors[nameof(request.DisplayName)] = ["Informe o nome da pessoa."];
        else if (request.DisplayName.Trim().Length > 200) errors[nameof(request.DisplayName)] = ["O nome deve ter até 200 caracteres."];
        if (string.IsNullOrWhiteSpace(request.Email) || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))
            errors[nameof(request.Email)] = ["Informe um endereço de e-mail válido."];
        if (request.Role is null || !new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator }
                .Contains(request.Role.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(request.Role)] = ["Selecione Administrador, Gestor ou Operador."];
        if (string.IsNullOrEmpty(request.TemporaryPassword)) errors[nameof(request.TemporaryPassword)] = ["Defina uma senha temporária para entregar fora do sistema."];
        return errors;
    }

    private static async Task<IResult> UpdateAsync(Guid userId, UpdateTeamMemberRequest request,
        ICurrentOrganizationContext context,
        UserManager<ApplicationUser> userManager,
        PitCrmDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var role = new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator }
            .Single(value => string.Equals(value, request.Role!.Trim(), StringComparison.OrdinalIgnoreCase));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var organization = await dbContext.Organizations
            .FromSqlInterpolated($"SELECT * FROM \"Organizations\" WHERE \"Id\" = {context.OrganizationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (organization is null) return Results.Unauthorized();

        var user = await dbContext.Users.SingleOrDefaultAsync(
            value => value.Id == userId && value.OrganizationId == context.OrganizationId, cancellationToken);
        if (user is null) return Results.NotFound();

        var currentRoles = await userManager.GetRolesAsync(user);
        var remainsActiveAdministrator = request.IsActive == true && role == RoleNames.Administrator;
        if (user.IsActive && !remainsActiveAdministrator && currentRoles.Contains(RoleNames.Administrator, StringComparer.Ordinal))
        {
            var otherActiveAdministrators = await dbContext.Users
                .Where(value => value.OrganizationId == context.OrganizationId && value.IsActive && value.Id != userId)
                .Join(dbContext.UserRoles, value => value.Id, userRole => userRole.UserId, (value, userRole) => userRole)
                .Join(dbContext.Roles.Where(value => value.Name == RoleNames.Administrator), userRole => userRole.RoleId,
                    roleEntity => roleEntity.Id, (_, _) => 1)
                .CountAsync(cancellationToken);
            if (otherActiveAdministrators == 0)
                return Results.Conflict(new { error = "A organização precisa manter pelo menos um Administrador ativo." });
        }

        user.DisplayName = request.DisplayName!.Trim();
        user.IsActive = request.IsActive!.Value;
        var emailResult = await userManager.SetEmailAsync(user, request.Email!.Trim());
        var userNameResult = emailResult.Succeeded
            ? await userManager.SetUserNameAsync(user, request.Email.Trim())
            : emailResult;
        if (!emailResult.Succeeded || !userNameResult.Succeeded)
        {
            if (emailResult.Errors.Concat(userNameResult.Errors)
                .Any(error => error.Code is "DuplicateUserName" or "DuplicateEmail"))
                return Results.Conflict(new { error = "Não foi possível atualizar essa conta. Confira os dados." });
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["Informe um endereço de e-mail válido."]
            });
        }

        if (!currentRoles.Contains(role, StringComparer.Ordinal))
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                return Results.Problem("Não foi possível atualizar o perfil da conta.");
            var addResult = await userManager.AddToRoleAsync(user, role);
            if (!addResult.Succeeded)
                return Results.Problem("Não foi possível atualizar o perfil da conta.");
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new TeamMemberDto(user.Id, user.DisplayName, user.Email!, role, user.IsActive, user.MustChangePassword));
    }

    private static Dictionary<string, string[]> Validate(UpdateTeamMemberRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.DisplayName)) errors[nameof(request.DisplayName)] = ["Informe o nome da pessoa."];
        else if (request.DisplayName.Trim().Length > 200) errors[nameof(request.DisplayName)] = ["O nome deve ter até 200 caracteres."];
        if (string.IsNullOrWhiteSpace(request.Email) || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))
            errors[nameof(request.Email)] = ["Informe um endereço de e-mail válido."];
        if (request.Role is null || !new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator }
                .Contains(request.Role.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(request.Role)] = ["Selecione Administrador, Gestor ou Operador."];
        if (request.IsActive is null) errors[nameof(request.IsActive)] = ["Informe se a conta está ativa."];
        return errors;
    }

    public sealed record CreateTeamMemberRequest(string? DisplayName, string? Email, string? Role, string? TemporaryPassword);
    public sealed record UpdateTeamMemberRequest(string? DisplayName, string? Email, string? Role, bool? IsActive);
    public sealed record TeamMemberDto(Guid Id, string DisplayName, string Email, string Role, bool IsActive, bool MustChangePassword);
    public sealed record TeamPage(TeamMemberDto[] Items, int Page, int PageSize, int TotalCount);
}
