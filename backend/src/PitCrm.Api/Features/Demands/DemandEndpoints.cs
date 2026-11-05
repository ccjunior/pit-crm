using PitCrm.Application.Authorization;
using PitCrm.Application.Demands;
using PitCrm.Domain.Demands;

namespace PitCrm.Api.Features.Demands;

public static class DemandEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/demands", CreateAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.OperateDemands);
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

        return Results.Created($"/api/demands/{demand.Id}", new DemandCreatedDto(demand.Id, demand.CustomerId,
            demand.LocationId, demand.Description, demand.Origin, demand.Priority, demand.Status,
            demand.TicketReference, demand.PurchaseOrderReference, demand.AssignedToUserId, demand.CreatedAtUtc));
    }

    private static Dictionary<string, string[]> Validate(CreateDemandRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.CustomerId == Guid.Empty) errors[nameof(request.CustomerId)] = ["Selecione o cliente."];
        if (request.LocationId == Guid.Empty) errors[nameof(request.LocationId)] = ["Selecione o local."];
        if (string.IsNullOrWhiteSpace(request.Description)) errors[nameof(request.Description)] = ["Informe a descrição da demanda."];
        else if (request.Description.Trim().Length > 4000) errors[nameof(request.Description)] = ["A descrição deve ter até 4.000 caracteres."];
        if (request.Origin is null || !Demand.Origins.Contains(request.Origin.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(request.Origin)] = ["Selecione uma origem válida: WhatsApp, Ligação, E-mail ou Outra."];
        if (request.Priority is not null && !Demand.Priorities.Contains(request.Priority.Trim(), StringComparer.OrdinalIgnoreCase))
            errors[nameof(request.Priority)] = ["A prioridade deve ser Baixa, Média ou Alta."];
        if (request.TicketReference?.Trim().Length > 100) errors[nameof(request.TicketReference)] = ["O chamado deve ter até 100 caracteres."];
        if (request.PurchaseOrderReference?.Trim().Length > 100) errors[nameof(request.PurchaseOrderReference)] = ["A ordem de compra deve ter até 100 caracteres."];
        return errors;
    }

    public sealed record CreateDemandRequest(Guid CustomerId, Guid LocationId, string? Description, string? Origin,
        Guid? AssignedToUserId, string? TicketReference, string? PurchaseOrderReference, string? Priority);
    public sealed record DemandCreatedDto(Guid Id, Guid CustomerId, Guid LocationId, string Description, string Origin,
        string? Priority, string Status, string? TicketReference, string? PurchaseOrderReference,
        Guid? AssignedToUserId, DateTimeOffset CreatedAtUtc);
}
