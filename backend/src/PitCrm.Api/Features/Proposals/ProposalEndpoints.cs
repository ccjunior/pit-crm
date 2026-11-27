using PitCrm.Application.Authorization;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;

namespace PitCrm.Api.Features.Proposals;

public static class ProposalEndpoints
{
    private const decimal MaxAmount = 9999999999999999.99m;

    public static void Map(WebApplication app)
    {
        var proposals = app.MapGroup("/api/proposals").RequireAuthorization(AuthorizationPolicies.ManageProposals);
        proposals.MapGet("", SearchAsync);
        proposals.MapGet("/{id:guid}", FindAsync);
        proposals.MapPost("", CreateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPut("/{id:guid}", UpdateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/revisions", CreateRevisionAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/send", RecordSentAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/approve", ApproveAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
    }

    private static async Task<IResult> SearchAsync(string? q, string? status, Guid? customerId,
        DateOnly? from, DateOnly? to, int? page, int? pageSize,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1 || (long)(currentPage - 1) * currentPageSize > int.MaxValue)
            errors[nameof(page)] = ["A página deve ser positiva e estar dentro do limite permitido."];
        if (currentPageSize is < 1 or > 100) errors[nameof(pageSize)] = ["O tamanho deve estar entre 1 e 100."];
        if (status is not null && !CommercialRequest.Statuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(status)] = ["A etapa informada não é válida."];
        if (from is { } start && to is { } end && start > end)
            errors[nameof(from)] = ["A data inicial deve ser anterior à data final."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var normalizedStatus = status is null ? null : CommercialRequest.Statuses.Single(value =>
            string.Equals(value, status.Trim(), StringComparison.OrdinalIgnoreCase));
        var result = await store.SearchAsync(context.OrganizationId, q, normalizedStatus, customerId,
            from, to, currentPage, currentPageSize, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> FindAsync(Guid id, ICurrentOrganizationContext context,
        IProposalStore store, CancellationToken cancellationToken)
    {
        var proposal = await store.FindAsync(context.OrganizationId, id, cancellationToken);
        return proposal is null ? Results.NotFound() : Results.Ok(proposal);
    }

    private static async Task<IResult> CreateAsync(CreateCommercialRequestInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var errors = ValidateRequest(request.CustomerId, request.ProspectName, request.Origin, request.Description);
        if (request.ProspectContactName?.Trim().Length > 200) errors[nameof(request.ProspectContactName)] = ["O contato deve ter até 200 caracteres."];
        if (request.ProspectEmail?.Trim().Length > 254) errors[nameof(request.ProspectEmail)] = ["O e-mail deve ter até 254 caracteres."];
        if (request.ProspectPhone?.Trim().Length > 40) errors[nameof(request.ProspectPhone)] = ["O telefone deve ter até 40 caracteres."];
        ValidateReferences(request.References, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.CreateAsync(context.OrganizationId, context.UserId, request,
            DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure == ProposalFailure.CustomerNotFound) return CustomerNotFound();
        if (result.Failure != ProposalFailure.None) return Results.ValidationProblem(GeneralError("Não foi possível registrar a solicitação."));
        return Results.Created($"/api/proposals/{result.Proposal!.Id}", result.Proposal);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateCommercialRequestInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var errors = ValidateRequest(request.CustomerId, request.ProspectName, request.Origin, request.Description);
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        ValidateReferences(request.References, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.UpdateAsync(context.OrganizationId, id, context.UserId, request,
            DateTimeOffset.UtcNow, cancellationToken);
        return MapResult(result);
    }

    private static async Task<IResult> CreateRevisionAsync(Guid id, CreateProposalRevisionInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        ValidateItems(request.Items, errors);
        if (request.Notes?.Trim().Length > 4000) errors[nameof(request.Notes)] = ["As observações devem ter até 4.000 caracteres."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.CreateRevisionAsync(context.OrganizationId, id, context.UserId,
            request, DateTimeOffset.UtcNow, cancellationToken);
        return MapResult(result);
    }

    private static async Task<IResult> RecordSentAsync(Guid id, RecordProposalSentInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        if (request.RevisionId == Guid.Empty) errors[nameof(request.RevisionId)] = ["Selecione a revisão enviada."];
        ValidateText(request.Channel, 1, 50, nameof(request.Channel), "Informe o canal de envio.", errors);
        if (request.SentAtUtc == default) errors[nameof(request.SentAtUtc)] = ["Informe quando a proposta foi enviada."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.RecordSentAsync(context.OrganizationId, id, context.UserId,
            request, DateTimeOffset.UtcNow, cancellationToken);
        return MapResult(result);
    }

    private static async Task<IResult> ApproveAsync(Guid id, RecordProposalApprovalInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        if (request.RevisionId == Guid.Empty) errors[nameof(request.RevisionId)] = ["Selecione a revisão aprovada."];
        ValidateText(request.ApproverName, 1, 200, nameof(request.ApproverName), "Informe quem aprovou pelo cliente.", errors);
        ValidateText(request.Channel, 1, 50, nameof(request.Channel), "Informe o canal em que o cliente aprovou.", errors);
        if (request.Notes?.Trim().Length > 2000) errors[nameof(request.Notes)] = ["A observação deve ter até 2.000 caracteres."];
        if (request.ApprovedAtUtc == default) errors[nameof(request.ApprovedAtUtc)] = ["Informe a data de aprovação."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.ApproveAsync(context.OrganizationId, id, context.UserId,
            request, DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure == ProposalFailure.None)
            return Results.Ok(new { proposal = result.Proposal, demandDraftId = result.DemandDraftId });
        return MapResult(result);
    }

    private static Dictionary<string, string[]> ValidateRequest(Guid? customerId, string? prospectName,
        string? origin, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (customerId is null) ValidateText(prospectName, 1, 200, nameof(prospectName), "Informe o nome do prospect.", errors);
        if (origin is null || !CommercialRequest.Origins.Contains(origin.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(origin)] = ["Selecione um canal de entrada válido."];
        ValidateText(description, 1, 4000, nameof(description), "Informe o resumo do serviço solicitado.", errors);
        return errors;
    }

    private static void ValidateReferences(IReadOnlyList<CommercialReferenceInput>? references, Dictionary<string, string[]> errors)
    {
        if (references is null) return;
        if (references.Count > 20) errors[nameof(references)] = ["Informe no máximo 20 referências."];
        for (var i = 0; i < Math.Min(references.Count, 20); i++)
        {
            var reference = references[i];
            if (reference is null || !CommercialRequest.ReferenceTypes.Contains(reference.Type?.Trim(), StringComparer.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(reference.Value) || reference.Value.Trim().Length > 120)
                errors[$"References[{i}]"] = ["Selecione um tipo de referência e informe seu valor (até 120 caracteres)."];
        }
    }

    private static void ValidateItems(IReadOnlyList<ProposalItemInput>? items, Dictionary<string, string[]> errors)
    {
        if (items is null || items.Count is < 1 or > 100)
        {
            errors[nameof(items)] = ["Informe de 1 a 100 itens."];
            return;
        }
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var field = $"Items[{index}]";
            if (string.IsNullOrWhiteSpace(item.Description)) errors[$"{field}.Description"] = ["Informe a descrição do item."];
            else if (item.Description.Trim().Length > 1000) errors[$"{field}.Description"] = ["A descrição deve ter até 1.000 caracteres."];
            if (item.Amount < 0 || item.Amount > MaxAmount || decimal.Round(item.Amount, 2) != item.Amount)
                errors[$"{field}.Amount"] = ["O valor deve ser não negativo, ter até duas casas decimais e caber no limite permitido."];
        }
    }

    private static void ValidateText(string? value, int minLength, int maxLength, string field,
        string requiredMessage, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[field] = [requiredMessage];
        else if (value.Trim().Length < minLength || value.Trim().Length > maxLength)
            errors[field] = [$"O campo deve ter entre {minLength} e {maxLength} caracteres."];
    }

    private static IResult MapResult(ProposalCommandResult result) => result.Failure switch
    {
        ProposalFailure.NotFound => Results.NotFound(),
        ProposalFailure.VersionConflict => Results.Conflict(new { error = "A solicitação foi alterada. Atualize os dados antes de tentar novamente." }),
        ProposalFailure.InvalidState => Results.Conflict(new { error = "A ação não é permitida na etapa atual." }),
        ProposalFailure.DemandAlreadyConverted => Results.Conflict(new { error = "A demanda já foi confirmada; use o fluxo de aditivo para alterar escopo/valor." }),
        ProposalFailure.ResponsibleRequired => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["responsibleUserId"] = ["Atribua um responsável comercial antes de iniciar a avaliação."]
        }),
        ProposalFailure.CustomerNotFound => CustomerNotFound(),
        ProposalFailure.InvalidData => Results.ValidationProblem(GeneralError("Revise os dados informados.")),
        _ => Results.Ok(result.Proposal),
    };

    private static IResult CustomerNotFound() => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["customerId"] = ["O cliente não foi encontrado na organização atual."] });

    private static Dictionary<string, string[]> GeneralError(string message) =>
        new() { ["request"] = [message] };
}
