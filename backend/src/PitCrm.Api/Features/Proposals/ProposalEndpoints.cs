using PitCrm.Application.Authorization;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;
using System.Text.Json;

namespace PitCrm.Api.Features.Proposals;

public static class ProposalEndpoints
{
    private const decimal MaxAmount = 9999999999999999.99m;

    public static void Map(WebApplication app)
    {
        var proposals = app.MapGroup("/api/proposals").RequireAuthorization(AuthorizationPolicies.ManageProposals);
        proposals.MapGet("", SearchAsync);
        proposals.MapGet("/kanban", KanbanAsync);
        proposals.MapGet("/{id:guid}", FindAsync);
        proposals.MapPost("", CreateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPut("/{id:guid}", UpdateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPut("/{id:guid}/tracking", UpdateTrackingAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/status", ChangeStatusAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/revisions", CreateRevisionAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/send", RecordSentAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        proposals.MapPost("/{id:guid}/approve", ApproveAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>();
        app.MapGet("/api/proposals/my-actions", MyActionsAsync)
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
    }

    private static async Task<IResult> SearchAsync(string? q, string? status, Guid? customerId,
        Guid? responsibleUserId, string? origin, string? overdueFor, DateOnly? from, DateOnly? to,
        string? sortBy, string? sortDirection, int? page, int? pageSize,
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
        if (to == DateOnly.MaxValue) errors[nameof(to)] = ["A data final está fora do intervalo permitido."];
        ValidateSearchFilters(origin, overdueFor, errors);
        ValidateSort(sortBy, sortDirection, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var normalizedStatus = status is null ? null : CommercialRequest.Statuses.Single(value =>
            string.Equals(value, status.Trim(), StringComparison.OrdinalIgnoreCase));
        var result = await store.SearchAsync(context.OrganizationId, q, normalizedStatus, customerId,
            responsibleUserId, NormalizeOrigin(origin), overdueFor?.ToLowerInvariant(), from, to,
            NormalizeSort(sortBy), !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase),
            currentPage, currentPageSize, LocalToday(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> KanbanAsync(string? q, Guid? customerId, Guid? responsibleUserId,
        string? origin, string? overdueFor, DateOnly? from, DateOnly? to, string? sortBy,
        string? sortDirection, int? pageSizePerColumn,
        string? cursors, ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var size = pageSizePerColumn ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (size is < 1 or > 100) errors[nameof(pageSizePerColumn)] = ["O tamanho por etapa deve estar entre 1 e 100."];
        if (from is { } start && to is { } end && start > end) errors[nameof(from)] = ["A data inicial deve ser anterior à data final."];
        if (to == DateOnly.MaxValue) errors[nameof(to)] = ["A data final está fora do intervalo permitido."];
        ValidateSearchFilters(origin, overdueFor, errors);
        ValidateSort(sortBy, sortDirection, errors);
        IReadOnlyDictionary<string, string?> cursorMap = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(cursors))
        {
            try { cursorMap = JsonSerializer.Deserialize<Dictionary<string, string?>>(Convert.FromBase64String(cursors)) ?? new Dictionary<string, string?>(); }
            catch (Exception exception) when (exception is FormatException or JsonException)
            { errors[nameof(cursors)] = ["Os cursores informados são inválidos."]; }
        }
        if (cursorMap.Keys.Any(key => !CommercialRequest.Statuses.Contains(key, StringComparer.Ordinal)))
            errors[nameof(cursors)] = ["O cursor contém uma etapa desconhecida."];
        if (cursorMap.Values.Any(value => !IsValidCursor(value)))
            errors[nameof(cursors)] = ["O cursor contém uma posição inválida."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.KanbanAsync(context.OrganizationId, q, customerId, responsibleUserId,
            NormalizeOrigin(origin), overdueFor?.ToLowerInvariant(), from, to, NormalizeSort(sortBy),
            !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase), size, cursorMap,
            LocalToday(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> MyActionsAsync(int? page, int? pageSize,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var size = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1 || (long)(currentPage - 1) * size > int.MaxValue) errors[nameof(page)] = ["A página deve ser positiva."];
        if (size is < 1 or > 100) errors[nameof(pageSize)] = ["O tamanho deve estar entre 1 e 100."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Results.Ok(await store.MyActionsAsync(context.OrganizationId, context.UserId, currentPage, size,
            LocalToday(), cancellationToken));
    }

    private static async Task<IResult> UpdateTrackingAsync(Guid id, ProposalTrackingInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão deve ser positiva."];
        var hasAction = request.NextAction is not null;
        var hasDescription = !string.IsNullOrWhiteSpace(request.NextAction?.Description);
        var hasTime = !string.IsNullOrWhiteSpace(request.NextAction?.ScheduledAtLocal);
        if (hasAction && hasDescription != hasTime) errors["nextAction"] = ["Descrição e horário da próxima ação devem ser informados juntos; envie null para remover."];
        if (hasAction && !hasDescription) errors["nextAction.description"] = ["Informe a descrição da próxima ação."];
        if (request.NextAction?.Description?.Trim().Length > 500) errors["nextAction.description"] = ["A descrição deve ter até 500 caracteres."];
        DateTimeOffset? scheduledAtUtc = null;
        if (hasTime)
        {
            if (OrganizationLocalScheduleTime.TryConvert(request.NextAction!.ScheduledAtLocal,
                    request.NextAction.SelectedOffset, out var converted, out var failedField))
                scheduledAtUtc = converted;
            else
            {
                var key = $"nextAction.{failedField}";
                errors[key] = failedField == "selectedOffset"
                    ? ["Horário ambíguo exige um offset válido para o fuso organizacional."]
                    : ["Informe um horário local válido no formato yyyy-MM-ddTHH:mm:ss; horários inexistentes são rejeitados."];
            }
        }
        if (!hasAction && request.NextAction?.SelectedOffset is not null) errors["nextAction.selectedOffset"] = ["O offset só pode ser informado junto com a próxima ação."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await store.UpdateTrackingAsync(context.OrganizationId, id, request.Version,
            request.CustomerDeadline, request.InternalDeadline, hasDescription ? request.NextAction!.Description!.Trim() : null,
            scheduledAtUtc, DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure == ProposalFailure.VersionConflict)
            return Results.Conflict(new { error = "A solicitação foi alterada. Atualize os dados antes de tentar novamente." });
        return result.Failure switch
        {
            ProposalFailure.NotFound => Results.NotFound(),
            ProposalFailure.VersionConflict => Results.Conflict(new { error = "A solicitação foi alterada. Atualize os dados antes de tentar novamente." }),
            ProposalFailure.InvalidData => Results.ValidationProblem(GeneralError("Revise os dados informados.")),
            _ => Results.Ok(result.Tracking),
        };
    }

    private static DateOnly LocalToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(OrganizationLocalScheduleTime.TimeZoneId)).DateTime);

    private static void ValidateSearchFilters(string? origin, string? overdueFor, Dictionary<string, string[]> errors)
    {
        if (origin is not null && !CommercialRequest.Origins.Contains(origin.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(origin)] = ["A origem informada não é válida."];
        if (overdueFor is not null && !new[] { "customer", "internal", "either" }.Contains(overdueFor, StringComparer.OrdinalIgnoreCase))
            errors[nameof(overdueFor)] = ["Use customer, internal ou either."];
    }

    private static string? NormalizeOrigin(string? origin) => origin is null ? null :
        CommercialRequest.Origins.Single(value => string.Equals(value, origin.Trim(), StringComparison.OrdinalIgnoreCase));

    private static void ValidateSort(string? sortBy, string? sortDirection, Dictionary<string, string[]> errors)
    {
        var allowed = new[] { "updatedAtUtc", "customerDeadline", "internalDeadline", "nextActionAtUtc", "number" };
        if (sortBy is not null && !allowed.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            errors[nameof(sortBy)] = ["A ordenação informada não é válida."];
        if (sortDirection is not null && sortDirection is not ("asc" or "desc")
            && !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            errors[nameof(sortDirection)] = ["Use asc ou desc."];
    }

    private static string NormalizeSort(string? sortBy) => sortBy?.ToLowerInvariant() switch
    {
        "customerdeadline" => "customerDeadline",
        "internaldeadline" => "internalDeadline",
        "nextactionatutc" => "nextActionAtUtc",
        "number" => "number",
        _ => "updatedAtUtc",
    };

    private static bool IsValidCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return int.TryParse(decoded, out var page) && page >= 1 && page <= int.MaxValue / 100;
        }
        catch (FormatException) { return false; }
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

    private static async Task<IResult> ChangeStatusAsync(Guid id, ProposalOutcomeInput request,
        ICurrentOrganizationContext context, IProposalStore store, CancellationToken cancellationToken)
    {
        if (request.Version < 1 || request.Status is null)
            return Results.ValidationProblem(GeneralError("Informe uma versão e uma etapa válida."));
        if (request.Status == CommercialRequest.NotQuoted && string.IsNullOrWhiteSpace(request.Reason))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Reason)] = ["Informe o motivo para não cotar."] });
        if (request.Reason?.Trim().Length > 1000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Reason)] = ["O motivo deve ter até 1.000 caracteres."] });
        var result = await store.ChangeStatusAsync(context.OrganizationId, id, context.UserId,
            request, DateTimeOffset.UtcNow, cancellationToken);
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
