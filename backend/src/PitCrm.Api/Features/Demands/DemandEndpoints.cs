using PitCrm.Application.Authorization;
using PitCrm.Application.Demands;
using PitCrm.Domain.Demands;

namespace PitCrm.Api.Features.Demands;

public static class DemandEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/demands", SearchAsync)
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
        app.MapGet("/api/demands/{id:guid}", FindAsync)
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
        app.MapPost("/api/demands", CreateAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
        app.MapPut("/api/demands/{id:guid}", UpdateAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
    }

    private static async Task<IResult> SearchAsync(string? q, Guid? customerId, Guid? locationId,
        Guid? assignedToUserId, string? status, string? priority, string? sortBy,
        int? page, int? pageSize, ICurrentOrganizationContext context,
        IDemandStore store, CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1 || (long)(currentPage - 1) * currentPageSize > int.MaxValue)
            errors[nameof(page)] = ["A página deve ser positiva e estar dentro do limite permitido."];
        if (currentPageSize is < 1 or > 100) errors[nameof(pageSize)] = ["O tamanho da página deve estar entre 1 e 100."];
        if (!string.IsNullOrWhiteSpace(status) && !Demand.Statuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(status)] = ["O status informado não é válido."];
        if (!string.IsNullOrWhiteSpace(priority) && !Demand.Priorities.Contains(priority.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(priority)] = ["A prioridade informada não é válida."];
        var order = string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : sortBy.Trim();
        if (order is not ("createdAt" or "priority")) errors[nameof(sortBy)] = ["A ordenação deve ser createdAt ou priority."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null
            : Demand.Statuses.Single(value => string.Equals(value, status.Trim(), StringComparison.OrdinalIgnoreCase));
        var normalizedPriority = string.IsNullOrWhiteSpace(priority) ? null
            : Demand.Priorities.Single(value => string.Equals(value, priority.Trim(), StringComparison.OrdinalIgnoreCase));
        var result = await store.SearchAsync(context.OrganizationId, q, customerId, locationId,
            assignedToUserId, normalizedStatus, normalizedPriority, order, currentPage, currentPageSize, cancellationToken);
        return Results.Ok(new DemandPageResponse(result.Items, currentPage, currentPageSize, result.TotalCount));
    }

    private static async Task<IResult> FindAsync(Guid id, ICurrentOrganizationContext context,
        IDemandStore store, CancellationToken cancellationToken)
    {
        var demand = await store.FindAsync(context.OrganizationId, id, cancellationToken);
        return demand is null ? Results.NotFound() : Results.Ok(demand);
    }

    private static async Task<IResult> CreateAsync(CreateDemandRequest request, ICurrentOrganizationContext context,
        IDemandStore store, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var now = DateTimeOffset.UtcNow;
        var origin = Demand.Origins.Single(value => string.Equals(value, request.Origin!.Trim(), StringComparison.OrdinalIgnoreCase));
        var priority = request.Priority is null ? null
            : Demand.Priorities.Single(value => string.Equals(value, request.Priority.Trim(), StringComparison.OrdinalIgnoreCase));
        var demand = new Demand(Guid.NewGuid(), context.OrganizationId, request.CustomerId, request.LocationId,
            context.UserId, request.AssignedToUserId, request.Description!, origin, priority,
            request.TicketReference, request.PurchaseOrderReference, now);
        var activity = new DemandActivity(Guid.NewGuid(), context.OrganizationId, demand.Id, context.UserId,
            "DemandCreated", "Demanda criada.", now);
        var result = await store.CreateAsync(demand, activity, cancellationToken);
        if (result.Failure != DemandCreateFailure.None)
        {
            var field = result.Failure switch
            {
                DemandCreateFailure.CustomerNotFound => nameof(request.CustomerId),
                DemandCreateFailure.LocationNotFound => nameof(request.LocationId),
                _ => nameof(request.AssignedToUserId),
            };
            var message = result.Failure switch
            {
                DemandCreateFailure.CustomerNotFound => "O cliente não foi encontrado na organização atual.",
                DemandCreateFailure.LocationNotFound => "O local não pertence ao cliente informado ou não existe.",
                _ => "O responsável deve ser um usuário ativo da organização atual.",
            };
            return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
        }

        return Results.Created($"/api/demands/{demand.Id}", new DemandCreatedDto(demand.Id, demand.ReferenceNumber, demand.Version,
            demand.CustomerId, demand.LocationId, demand.Description, demand.Origin, demand.Priority, demand.Status,
            demand.TicketReference, demand.PurchaseOrderReference, demand.AssignedToUserId, demand.CreatedAtUtc));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateDemandRequest request,
        ICurrentOrganizationContext context, IDemandStore store, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (request.Version < 1) errors[nameof(request.Version)] = ["A versão da demanda deve ser positiva."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var origin = Demand.Origins.Single(value => string.Equals(value, request.Origin!.Trim(), StringComparison.OrdinalIgnoreCase));
        var priority = request.Priority is null ? null
            : Demand.Priorities.Single(value => string.Equals(value, request.Priority.Trim(), StringComparison.OrdinalIgnoreCase));
        var update = new DemandUpdateData(request.Version, request.CustomerId, request.LocationId,
            request.Description!, origin, priority, request.TicketReference, request.PurchaseOrderReference,
            request.AssignedToUserId);
        var result = await store.UpdateAsync(context.OrganizationId, id, context.UserId, update,
            DateTimeOffset.UtcNow, cancellationToken);
        if (result.Failure == DemandUpdateFailure.DemandNotFound) return Results.NotFound();
        if (result.Failure == DemandUpdateFailure.VersionConflict)
            return Results.Conflict(new { error = "A demanda foi alterada por outra pessoa. Recarregue os dados atuais antes de salvar." });
        if (result.Failure != DemandUpdateFailure.None)
        {
            var field = result.Failure switch
            {
                DemandUpdateFailure.CustomerNotFound => nameof(request.CustomerId),
                DemandUpdateFailure.LocationNotFound => nameof(request.LocationId),
                _ => nameof(request.AssignedToUserId),
            };
            var message = result.Failure switch
            {
                DemandUpdateFailure.CustomerNotFound => "O cliente não foi encontrado na organização atual.",
                DemandUpdateFailure.LocationNotFound => "O local não pertence ao cliente informado ou não existe.",
                _ => "O responsável deve ser um usuário ativo da organização atual.",
            };
            return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
        }

        var demand = result.Demand!;
        return Results.Ok(new DemandUpdatedDto(demand.Id, demand.ReferenceNumber, demand.Version,
            demand.CustomerId, demand.LocationId, demand.Description, demand.Origin, demand.Priority,
            demand.Status, demand.TicketReference, demand.PurchaseOrderReference, demand.AssignedToUserId));
    }

    private static Dictionary<string, string[]> Validate(CreateDemandRequest request) =>
        ValidateDemandData(request.CustomerId, request.LocationId, request.Description, request.Origin,
            request.Priority, request.TicketReference, request.PurchaseOrderReference);

    private static Dictionary<string, string[]> Validate(UpdateDemandRequest request) =>
        ValidateDemandData(request.CustomerId, request.LocationId, request.Description, request.Origin,
            request.Priority, request.TicketReference, request.PurchaseOrderReference);

    private static Dictionary<string, string[]> ValidateDemandData(Guid customerId, Guid locationId,
        string? description, string? origin, string? priority, string? ticketReference, string? purchaseOrderReference)
    {
        var errors = new Dictionary<string, string[]>();
        if (customerId == Guid.Empty) errors[nameof(CreateDemandRequest.CustomerId)] = ["Selecione o cliente."];
        if (locationId == Guid.Empty) errors[nameof(CreateDemandRequest.LocationId)] = ["Selecione o local."];
        if (string.IsNullOrWhiteSpace(description)) errors[nameof(CreateDemandRequest.Description)] = ["Informe a descrição da demanda."];
        else if (description.Trim().Length > 4000) errors[nameof(CreateDemandRequest.Description)] = ["A descrição deve ter até 4.000 caracteres."];
        if (origin is null || !Demand.Origins.Contains(origin.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(CreateDemandRequest.Origin)] = ["Selecione uma origem válida: WhatsApp, Ligação, E-mail ou Outra."];
        if (priority is not null && !Demand.Priorities.Contains(priority.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(CreateDemandRequest.Priority)] = ["A prioridade deve ser Baixa, Média ou Alta."];
        if (ticketReference?.Trim().Length > 100) errors[nameof(CreateDemandRequest.TicketReference)] = ["O chamado deve ter até 100 caracteres."];
        if (purchaseOrderReference?.Trim().Length > 100) errors[nameof(CreateDemandRequest.PurchaseOrderReference)] = ["A ordem de compra deve ter até 100 caracteres."];
        return errors;
    }

    public sealed record CreateDemandRequest(Guid CustomerId, Guid LocationId, string? Description, string? Origin,
        Guid? AssignedToUserId, string? TicketReference, string? PurchaseOrderReference, string? Priority);
    public sealed record UpdateDemandRequest(int Version, Guid CustomerId, Guid LocationId, string? Description, string? Origin,
        Guid? AssignedToUserId, string? TicketReference, string? PurchaseOrderReference, string? Priority);
    public sealed record DemandCreatedDto(Guid Id, int ReferenceNumber, int Version, Guid CustomerId, Guid LocationId, string Description, string Origin,
        string? Priority, string Status, string? TicketReference, string? PurchaseOrderReference,
        Guid? AssignedToUserId, DateTimeOffset CreatedAtUtc);
    public sealed record DemandUpdatedDto(Guid Id, int ReferenceNumber, int Version, Guid CustomerId, Guid LocationId,
        string Description, string Origin, string? Priority, string Status, string? TicketReference,
        string? PurchaseOrderReference, Guid? AssignedToUserId);
    public sealed record DemandPageResponse(IReadOnlyList<DemandListItemDto> Items, int Page, int PageSize, int TotalCount);
}
