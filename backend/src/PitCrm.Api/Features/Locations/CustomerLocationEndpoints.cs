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
        if (request.ResponsibleContactId is { } contactId
            && !await store.ResponsibleContactExistsAsync(context.OrganizationId, customerId, null, contactId, cancellationToken))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.ResponsibleContactId)] = ["Selecione um contato deste cliente."] });
        var now = DateTimeOffset.UtcNow;
        var location = new CustomerLocation(Guid.NewGuid(), context.OrganizationId, customerId, context.UserId,
            request.Name!, request.Address!, request.Neighborhood, request.City, request.MapUrl, request.Instructions, now);
        location.SetResponsibleContact(context.UserId, request.ResponsibleContactId, now);
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
        if (request.ResponsibleContactId is { } contactId
            && !await store.ResponsibleContactExistsAsync(context.OrganizationId, customerId, location.Id, contactId, cancellationToken))
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.ResponsibleContactId)] = ["Selecione um contato do cliente ou deste local."] });
        location.UpdateDetails(context.UserId, request.Name!, request.Address!, request.Neighborhood, request.City,
            request.MapUrl, ParseStatus(request.Status), request.Instructions, DateTimeOffset.UtcNow);
        location.SetResponsibleContact(context.UserId, request.ResponsibleContactId, DateTimeOffset.UtcNow);
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
        if (request.Neighborhood?.Trim().Length > 200) errors[nameof(request.Neighborhood)] = ["O bairro deve ter até 200 caracteres."];
        if (request.City?.Trim().Length > 200) errors[nameof(request.City)] = ["A cidade deve ter até 200 caracteres."];
        if (request.MapUrl?.Trim().Length > 2000) errors[nameof(request.MapUrl)] = ["O link do Google Maps deve ter até 2000 caracteres."];
        else if (!string.IsNullOrWhiteSpace(request.MapUrl) && !IsGoogleMapsUrl(request.MapUrl))
            errors[nameof(request.MapUrl)] = ["Informe um link HTTPS válido do Google Maps."];
        if (request.Status is not null && !Enum.TryParse<CustomerLocationStatus>(request.Status, true, out _))
            errors[nameof(request.Status)] = ["Informe um status válido: Active, Paused ou Completed."];
        return errors;
    }

    private static CustomerLocationDto ToDto(CustomerLocation location) => new(location.Id, location.CustomerId,
        location.Name, location.Address, location.Neighborhood, location.City, location.Status.ToString(), location.Instructions,
        location.MapUrl, location.ResponsibleContactId, null, null, null,
        location.CreatedAtUtc, location.UpdatedAtUtc);

    private static bool IsGoogleMapsUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        return uri.IdnHost.ToLowerInvariant() switch
        {
            "maps.app.goo.gl" => true,
            "goo.gl" => uri.AbsolutePath.StartsWith("/maps/", StringComparison.OrdinalIgnoreCase),
            "google.com" or "www.google.com" => uri.AbsolutePath.StartsWith("/maps/", StringComparison.OrdinalIgnoreCase),
            "maps.google.com" => true,
            _ => false,
        };
    }

    private static CustomerLocationStatus ParseStatus(string? status) =>
        Enum.TryParse<CustomerLocationStatus>(status, true, out var parsed) ? parsed : CustomerLocationStatus.Active;

    public sealed record LocationRequest(string? Name, string? Address, string? Neighborhood, string? City, string? Status,
        string? Instructions, string? MapUrl, Guid? ResponsibleContactId);
}
