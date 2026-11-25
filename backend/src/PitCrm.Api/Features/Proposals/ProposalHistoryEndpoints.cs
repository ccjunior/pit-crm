using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitCrm.Application.Authorization;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;

namespace PitCrm.Api.Features.Proposals;

public static class ProposalHistoryEndpoints
{
    public static void Map(WebApplication app)
    {
        var proposals = app.MapGroup("/api/proposals").RequireAuthorization(AuthorizationPolicies.ManageProposals);
        proposals.MapGet("/{id:guid}/timeline", GetTimelineAsync);
        proposals.MapPost("/{id:guid}/status", ChangeStatusAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/activities", RecordActivityAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/comments", RecordCommentAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapGet("/reasons", GetReasonsAsync);
        app.MapPost("/api/proposals/reasons", CreateReasonAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageProposalReasons)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        app.MapPost("/api/proposals/reasons/{reasonId:guid}/deactivate", DeactivateReasonAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageProposalReasons)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        app.MapGet("/api/proposals/{id:guid}/audit", GetAuditAsync)
            .RequireAuthorization(AuthorizationPolicies.ViewProposalAudit);
        app.MapGet("/api/proposals/{id:guid}/timeline/archive", ReadArchiveAsync)
            .RequireAuthorization(AuthorizationPolicies.RestoreProposalTimeline);
        app.MapGet("/api/demands/{id:guid}/proposal-timeline", GetOperatorTimelineAsync)
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
    }

    private static async Task<IResult> GetTimelineAsync(Guid id, int? page, int? pageSize,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        if (!ValidPage(page, pageSize, out var currentPage, out var size, out var errors))
            return Results.ValidationProblem(errors);
        var result = await store.GetTimelineAsync(context.OrganizationId, id, currentPage, size, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetOperatorTimelineAsync(Guid id, int? page, int? pageSize,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        if (!ValidPage(page, pageSize, out var currentPage, out var size, out var errors))
            return Results.ValidationProblem(errors);
        var result = await store.GetOperatorTimelineAsync(context.OrganizationId, id, currentPage, size, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ChangeStatusAsync(Guid id, ProposalOutcomeInput request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentOrganizationContext context, IProposalHistoryStore historyStore,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        if (request.Status is null || !CommercialRequest.Statuses.Contains(request.Status, StringComparer.Ordinal))
            errors[nameof(request.Status)] = ["A etapa informada não é válida."];
        if (request.Reason is not null)
            errors[nameof(request.Reason)] = ["Use um motivo estruturado do catálogo; texto livre não é aceito nesta operação."];
        if (request.Status == CommercialRequest.NotQuoted && request.ReasonId is null)
            errors[nameof(request.ReasonId)] = ["Selecione um motivo ativo para Não cotada."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryIdempotencyKey(idempotencyKey, out var key)) return IdempotencyProblem();
        var result = await historyStore.ChangeStatusAsync(context.OrganizationId, id, context.UserId,
            key, request, DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure != ProposalFailure.None) return MapHistoryFailure(result.Failure, "reasonId");
        return result.Proposal is null ? Results.NotFound() : Results.Ok(result.Proposal);
    }

    private static Task<IResult> RecordActivityAsync(Guid id, ProposalActivityInput request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpContext httpContext, IAuthorizationService authorization,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken) =>
        RecordEventAsync(id, request, idempotencyKey, ProposalHistoryEventTypes.ActivityRecorded,
            httpContext, authorization, context, store, cancellationToken);

    private static Task<IResult> RecordCommentAsync(Guid id, ProposalActivityInput request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpContext httpContext, IAuthorizationService authorization,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken) =>
        RecordEventAsync(id, request, idempotencyKey, ProposalHistoryEventTypes.CommentAdded,
            httpContext, authorization, context, store, cancellationToken);

    private static async Task<IResult> RecordEventAsync(Guid id, ProposalActivityInput request,
        string? idempotencyKey, string eventType, HttpContext httpContext, IAuthorizationService authorization,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Text)) errors["text"] = ["Informe o conteúdo."];
        else if (request.Text.Trim().Length > 4000) errors["text"] = ["O conteúdo deve ter até 4.000 caracteres."];
        if (eventType == ProposalHistoryEventTypes.ActivityRecorded && string.IsNullOrWhiteSpace(request.Type))
            errors["type"] = ["Informe o tipo da atividade."];
        if (eventType == ProposalHistoryEventTypes.CommentAdded && request.Type is not null)
            errors["type"] = ["Comentários não recebem tipo de atividade."];
        if (request.MentionedUserIds is { Count: > 20 }) errors["mentionedUserIds"] = ["Mencione no máximo 20 pessoas."];
        if (request.MentionedUserIds?.Any(value => value == Guid.Empty) == true)
            errors["mentionedUserIds"] = ["A lista de menções contém um identificador inválido."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var mentions = request.MentionedUserIds?.Distinct().ToArray() ?? [];
        if (mentions.Length > 0 && !(await authorization.AuthorizeAsync(httpContext.User,
                AuthorizationPolicies.MentionProposalUsers)).Succeeded)
            return Results.Forbid();
        if (!TryIdempotencyKey(idempotencyKey, out var key)) return IdempotencyProblem();
        var result = eventType == ProposalHistoryEventTypes.ActivityRecorded
            ? await store.RecordActivityAsync(context.OrganizationId, id, context.UserId, key, request, DateTimeOffset.UtcNow, cancellationToken)
            : await store.RecordCommentAsync(context.OrganizationId, id, context.UserId, key, request, DateTimeOffset.UtcNow, cancellationToken);
        return result.Failure == ProposalFailure.None ? Results.Created($"/api/proposals/{id}/timeline", result.Event)
            : MapHistoryFailure(result.Failure, "mentionedUserIds");
    }

    private static async Task<IResult> GetReasonsAsync(string? resultStatus, ICurrentOrganizationContext context,
        IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        if (resultStatus is not null && !CommercialRequest.ResultStatuses.Contains(resultStatus, StringComparer.Ordinal))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(resultStatus)] = ["O resultado informado não é válido."] });
        return Results.Ok(await store.GetReasonsAsync(context.OrganizationId, resultStatus, cancellationToken));
    }

    private static async Task<IResult> CreateReasonAsync(ProposalReasonInput request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ResultStatus is null || !CommercialRequest.ResultStatuses.Contains(request.ResultStatus, StringComparer.Ordinal))
            errors[nameof(request.ResultStatus)] = ["Informe um resultado válido."];
        if (string.IsNullOrWhiteSpace(request.Label) || request.Label.Trim().Length > 120)
            errors[nameof(request.Label)] = ["O rótulo deve ter entre 1 e 120 caracteres."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!TryIdempotencyKey(idempotencyKey, out var key)) return IdempotencyProblem();
        var result = await store.CreateReasonAsync(context.OrganizationId, context.UserId, key, request,
            DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure != ProposalFailure.None)
            return MapHistoryFailure(result.Failure, "resultStatus");
        return result.Reason is null ? Results.NotFound()
            : Results.Created($"/api/proposals/reasons/{result.Reason.Id}", result.Reason);
    }

    private static async Task<IResult> DeactivateReasonAsync(Guid reasonId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(idempotencyKey, out var key)) return IdempotencyProblem();
        var result = await store.DeactivateReasonAsync(context.OrganizationId, reasonId, context.UserId,
            key, DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure != ProposalFailure.None)
            return MapHistoryFailure(result.Failure, "reasonId");
        return result.Reason is null ? Results.NotFound() : Results.Ok(result.Reason);
    }

    private static async Task<IResult> GetAuditAsync(Guid id, int? page, int? pageSize,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        if (!ValidPage(page, pageSize, out var currentPage, out var size, out var errors))
            return Results.ValidationProblem(errors);
        var result = await store.GetAuditAsync(context.OrganizationId, id, currentPage, size, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ReadArchiveAsync(Guid id, int? page, int? pageSize,
        ICurrentOrganizationContext context, IProposalHistoryStore store, CancellationToken cancellationToken)
    {
        if (!ValidPage(page, pageSize, out var currentPage, out var size, out var errors))
            return Results.ValidationProblem(errors);
        var result = await store.ReadArchiveAsync(context.OrganizationId, id, context.UserId,
            currentPage, size, DateTimeOffset.UtcNow, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static bool ValidPage(int? page, int? pageSize, out int currentPage, out int size,
        out Dictionary<string, string[]> errors)
    {
        currentPage = page ?? 1;
        size = pageSize ?? 20;
        errors = [];
        if (currentPage < 1 || (size > 0 && (long)(currentPage - 1) * size > int.MaxValue))
            errors["page"] = ["A página deve ser positiva e estar dentro do limite permitido."];
        if (size is < 1 or > 100) errors["pageSize"] = ["O tamanho deve estar entre 1 e 100."];
        return errors.Count == 0;
    }

    private static bool TryIdempotencyKey(string? value, out Guid key) =>
        Guid.TryParse(value, out key) && ProposalIdempotencyKey.IsWithinValidityWindow(key, DateTimeOffset.UtcNow);

    private static IResult IdempotencyProblem() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["Idempotency-Key"] = ["Envie uma chave UUIDv7 válida no cabeçalho Idempotency-Key."]
    });

    private static IResult MapHistoryFailure(ProposalFailure failure, string field) => failure switch
    {
        ProposalFailure.NotFound => Results.NotFound(),
        ProposalFailure.VersionConflict => Results.Conflict(new { error = "A chave foi reutilizada com outro payload ou a solicitação foi alterada." }),
        ProposalFailure.InvalidState => Results.Conflict(new { error = "A ação não é permitida na etapa atual." }),
        ProposalFailure.ResponsibleRequired => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["responsibleUserId"] = ["Atribua um responsável comercial antes de iniciar a avaliação."]
        }),
        ProposalFailure.InvalidData => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = ["Revise os dados informados."] }),
        _ => Results.Conflict(new { error = "A operação não pôde ser aplicada." })
    };
}
