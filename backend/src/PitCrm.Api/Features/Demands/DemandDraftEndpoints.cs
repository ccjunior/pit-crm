using System.Security.Claims;
using PitCrm.Api.Authorization;
using PitCrm.Application.Authorization;
using PitCrm.Application.Demands;

namespace PitCrm.Api.Features.Demands;

public static class DemandDraftEndpoints
{
    public static void Map(WebApplication app)
    {
        var drafts = app.MapGroup("/api/demand-drafts").RequireAuthorization(AuthorizationPolicies.OperateDemands);
        drafts.MapGet("", SearchAsync);
        drafts.MapGet("/{id:guid}", FindAsync);
        drafts.MapPut("/{id:guid}", UpdateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        drafts.MapPost("/{id:guid}/convert", ConvertAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
    }

    private static async Task<IResult> SearchAsync(string? q, int? page, int? pageSize,
        ICurrentOrganizationContext context, IDemandDraftStore store, CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        if (currentPage < 1 || (long)(currentPage - 1) * currentPageSize > int.MaxValue
            || currentPageSize is < 1 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["page"] = ["Informe uma página positiva e um tamanho entre 1 e 100."] });
        return Results.Ok(await store.SearchAsync(context.OrganizationId, q, currentPage,
            currentPageSize, cancellationToken));
    }

    private static async Task<IResult> FindAsync(Guid id, ICurrentOrganizationContext context,
        IDemandDraftStore store, CancellationToken cancellationToken)
    {
        var draft = await store.FindAsync(context.OrganizationId, id, cancellationToken);
        return draft is null ? Results.NotFound() : Results.Ok(draft);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateDemandDraftInput request,
        ClaimsPrincipal principal, ICurrentOrganizationContext context,
        IDemandDraftStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        if (string.IsNullOrWhiteSpace(request.Description)) errors[nameof(request.Description)] = ["Informe a descrição da demanda."];
        else if (request.Description.Trim().Length > 4000) errors[nameof(request.Description)] = ["A descrição deve ter até 4.000 caracteres."];
        if (request.Origin is null || !PitCrm.Domain.Demands.Demand.Origins.Contains(request.Origin.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(request.Origin)] = ["Selecione um canal válido."];
        if (request.Priority is not null && !PitCrm.Domain.Demands.Demand.Priorities.Contains(request.Priority.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(request.Priority)] = ["Selecione uma prioridade válida."];
        if (request.LocationId is not null && request.CustomerId is null)
            errors[nameof(request.LocationId)] = ["Selecione o cliente antes do local."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var mayManageCustomers = principal.IsInRole(RoleNames.Administrator) || principal.IsInRole(RoleNames.Manager);
        var result = await store.UpdateAsync(context.OrganizationId, id, context.UserId, request,
            mayManageCustomers, DateTimeOffset.UtcNow, cancellationToken);
        return result.Failure switch
        {
            DemandDraftFailure.NotFound => Results.NotFound(),
            DemandDraftFailure.VersionConflict => Results.Conflict(new { error = "O rascunho foi alterado por outra pessoa." }),
            DemandDraftFailure.AlreadyConverted => Results.Conflict(new { error = "O rascunho já foi convertido em demanda." }),
            DemandDraftFailure.CustomerNotFound => Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.CustomerId)] = ["O cliente não existe nesta organização."] }),
            DemandDraftFailure.LocationNotFound => Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.LocationId)] = ["O local não pertence ao cliente informado."] }),
            DemandDraftFailure.AssigneeNotEligible => Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.AssignedToUserId)] = ["O responsável não está ativo nesta organização."] }),
            DemandDraftFailure.NotAllowed => Results.Forbid(),
            DemandDraftFailure.InvalidData => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Os dados informados são inválidos."] }),
            _ => Results.Ok(result.Draft),
        };
    }

    private static async Task<IResult> ConvertAsync(Guid id, ConvertDemandDraftRequest request,
        ICurrentOrganizationContext context, IDemandDraftStore store, CancellationToken cancellationToken)
    {
        if (request.Version < 1)
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Version)] = ["A versão deve ser positiva."] });
        var result = await store.ConvertAsync(context.OrganizationId, id, context.UserId,
            request.Version, DateTimeOffset.UtcNow, cancellationToken);
        return result.Failure switch
        {
            DemandDraftFailure.NotFound => Results.NotFound(),
            DemandDraftFailure.VersionConflict => Results.Conflict(new { error = "O rascunho foi alterado. Atualize antes de confirmar." }),
            DemandDraftFailure.AlreadyConverted => Results.Conflict(new { error = "O rascunho já foi convertido." }),
            DemandDraftFailure.CustomerNotFound => Results.ValidationProblem(new Dictionary<string, string[]> { ["customerId"] = ["Vincule um cliente antes de confirmar a demanda."] }),
            DemandDraftFailure.LocationNotFound => Results.ValidationProblem(new Dictionary<string, string[]> { ["locationId"] = ["Vincule um local do cliente antes de confirmar a demanda."] }),
            DemandDraftFailure.AssigneeNotEligible => Results.ValidationProblem(new Dictionary<string, string[]> { ["assignedToUserId"] = ["O responsável não está ativo nesta organização."] }),
            _ => Results.Ok(new { demandId = result.DemandId }),
        };
    }

    public sealed record ConvertDemandDraftRequest(int Version);
}
