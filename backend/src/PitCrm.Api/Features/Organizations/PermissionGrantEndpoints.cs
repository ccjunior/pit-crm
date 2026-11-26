using Microsoft.EntityFrameworkCore;
using PitCrm.Application.Authorization;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Features.Organizations;

public static class PermissionGrantEndpoints
{
    private static readonly string[] ManagedPermissions =
        [AuthorizationPolicies.ViewProposalAuditGrant, AuthorizationPolicies.RestoreProposalTimelineGrant];

    public static void Map(WebApplication app)
    {
        var grants = app.MapGroup("/api/organization/permission-grants")
            .RequireAuthorization(AuthorizationPolicies.ManageOrganizationPermissionGrants);
        grants.MapPut("/{permission}/{userId:guid}", GrantAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        grants.MapPost("/{permission}/{userId:guid}/revoke", RevokeAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
    }

    private static async Task<IResult> GrantAsync(string permission, Guid userId, GrantJustification request,
        ICurrentOrganizationContext context, PitCrmDbContext dbContext, CancellationToken cancellationToken)
    {
        var validation = Validate(permission, userId, request.Justification);
        if (validation is not null) return validation;
        if (userId == context.UserId)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Não é permitido conceder permissão a si mesmo."]
            });
        var justification = request.Justification!.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireGrantLockAsync(context.OrganizationId, userId, permission, dbContext, cancellationToken);
        var recipientEligible = await dbContext.Users.AsNoTracking()
            .Where(user => user.OrganizationId == context.OrganizationId && user.Id == userId && user.IsActive)
            .Select(user => (bool?)dbContext.UserRoles.Where(link => link.UserId == user.Id)
                    .Join(dbContext.Roles, link => link.RoleId, role => role.Id, (_, role) => role.Name!)
                    .Any(role => role == RoleNames.Administrator || role == RoleNames.Manager))
            .SingleOrDefaultAsync(cancellationToken);
        if (recipientEligible is null) return Results.NotFound();
        if (!recipientEligible.Value) return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["userId"] = ["A permissão só pode ser concedida a Administrador ou Gestor ativo da organização."]
        });

        var existing = await dbContext.OrganizationPermissionGrants.SingleOrDefaultAsync(value =>
            value.OrganizationId == context.OrganizationId && value.UserId == userId
            && value.Permission == permission, cancellationToken);
        if (existing is null)
        {
            dbContext.OrganizationPermissionGrants.Add(new OrganizationPermissionGrant(
                context.OrganizationId, userId, permission));
            dbContext.OrganizationPermissionGrantAudits.Add(new OrganizationPermissionGrantAudit(
                context.OrganizationId, context.UserId, userId, permission, "Granted",
                DateTimeOffset.UtcNow, justification));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeAsync(string permission, Guid userId, GrantJustification request,
        ICurrentOrganizationContext context, PitCrmDbContext dbContext, CancellationToken cancellationToken)
    {
        var validation = Validate(permission, userId, request.Justification);
        if (validation is not null) return validation;
        var justification = request.Justification!.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireGrantLockAsync(context.OrganizationId, userId, permission, dbContext, cancellationToken);
        var grant = await dbContext.OrganizationPermissionGrants.SingleOrDefaultAsync(value =>
            value.OrganizationId == context.OrganizationId && value.UserId == userId
            && value.Permission == permission, cancellationToken);
        if (grant is not null)
        {
            dbContext.OrganizationPermissionGrants.Remove(grant);
            dbContext.OrganizationPermissionGrantAudits.Add(new OrganizationPermissionGrantAudit(
                context.OrganizationId, context.UserId, userId, permission, "Revoked",
                DateTimeOffset.UtcNow, justification));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult? Validate(string permission, Guid userId, string? justification)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ManagedPermissions.Contains(permission, StringComparer.Ordinal))
            errors["permission"] = ["A permissão não pode ser gerenciada por esta operação."];
        if (userId == Guid.Empty) errors["userId"] = ["Informe um usuário válido."];
        if (string.IsNullOrWhiteSpace(justification) || justification.Trim().Length > 500)
            errors["justification"] = ["Informe uma justificativa de até 500 caracteres."];
        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static Task<int> AcquireGrantLockAsync(Guid organizationId, Guid userId, string permission,
        PitCrmDbContext dbContext, CancellationToken cancellationToken)
    {
        var lockKey = $"permission-grant:{organizationId:N}:{userId:N}:{permission}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
    }

    public sealed record GrantJustification(string? Justification);
}
