using PitCrm.Application.Authorization;
using PitCrm.Application.Locations;
using PitCrm.Domain.Locations;

namespace PitCrm.Api.Features.Locations;

public static class CustomerLocationEndpoints
{
    public static void Map(WebApplication app)
    {
        var locations = app.MapGroup("/api/customers/{customerId:guid}/locations");
        locations.MapGet("", ListAsync).RequireAuthorization(AuthorizationPolicies.ReadCustomersAndLocations);
        locations.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.ReadCustomersAndLocations);
        locations.MapPost("", CreateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
        locations.MapPut("/{id:guid}", UpdateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
    }

    private static async Task<IResult> ListAsync(Guid customerId, ICurrentOrganizationContext context,
        ICustomerLocationStore store, CancellationToken cancellationToken)
    {
        if (!await store.CustomerExistsAsync(context.OrganizationId, customerId, cancellationToken)) return Results.NotFound();
        return Results.Ok(await store.ListAsync(context.OrganizationId, customerId, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid customerId, Guid id, ICurrentOrganizationContext context,
        ICustomerLocationStore store, CancellationToken cancellationToken)
    {
        if (!await store.CustomerExistsAsync(context.OrganizationId, customerId, cancellationToken)) return Results.NotFound();
        var location = await store.FindAsync(context.OrganizationId, customerId, id, cancellationToken);
        return location is null ? Results.NotFound() : Results.Ok(ToDto(location));
    }

    private static async Task<IResult> CreateAsync(Guid customerId, LocationRequest request,
        ICurrentOrganizationContext context, ICustomerLocationStore store, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!await store.CustomerExistsAsync(context.OrganizationId, customerId, cancellationToken)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var location = new CustomerLocation(Guid.NewGuid(), context.OrganizationId, customerId, context.UserId,
            request.Name!, request.Address!, request.Instructions, now);
        await store.AddAsync(location, cancellationToken);
        return Results.Created($"/api/customers/{customerId}/locations/{location.Id}", ToDto(location));
    }

    private static async Task<IResult> UpdateAsync(Guid customerId, Guid id, LocationRequest request,
        ICurrentOrganizationContext context, ICustomerLocationStore store, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var location = await store.FindAsync(context.OrganizationId, customerId, id, cancellationToken);
        if (location is null) return Results.NotFound();
        location.UpdateDetails(context.UserId, request.Name!, request.Address!, request.Instructions, DateTimeOffset.UtcNow);
        await store.UpdateAsync(location, cancellationToken);
        return Results.Ok(ToDto(location));
    }

    private static Dictionary<string, string[]> Validate(LocationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors[nameof(request.Name)] = ["Informe o nome do local."];
        else if (request.Name.Trim().Length > 200) errors[nameof(request.Name)] = ["O nome do local deve ter até 200 caracteres."];
        if (string.IsNullOrWhiteSpace(request.Address)) errors[nameof(request.Address)] = ["Informe o endereço do local."];
        else if (request.Address.Trim().Length > 500) errors[nameof(request.Address)] = ["O endereço deve ter até 500 caracteres."];
        if (request.Instructions?.Trim().Length > 2000) errors[nameof(request.Instructions)] = ["As instruções devem ter até 2000 caracteres."];
        return errors;
    }

    private static CustomerLocationDto ToDto(CustomerLocation location) => new(location.Id, location.CustomerId,
        location.Name, location.Address, location.Instructions, location.CreatedAtUtc, location.UpdatedAtUtc);

    public sealed record LocationRequest(string? Name, string? Address, string? Instructions);
}
