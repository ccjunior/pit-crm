using System.Net.Mail;
using PitCrm.Application.Authorization;
using PitCrm.Application.Customers;
using PitCrm.Domain.Customers;

namespace PitCrm.Api.Features.Customers;

public static class CustomerContactEndpoints
{
    public static void Map(WebApplication app)
    {
        var contacts = app.MapGroup("/api/customers/{customerId:guid}/contacts");
        contacts.MapGet("", ListAsync).RequireAuthorization(AuthorizationPolicies.ReadCustomersAndLocations);
        contacts.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.ReadCustomersAndLocations);
        contacts.MapPost("", CreateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
        contacts.MapPut("/{id:guid}", UpdateAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
        contacts.MapDelete("/{id:guid}", DeleteAsync).AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
    }

    private static async Task<IResult> ListAsync(Guid customerId, Guid? locationId, bool? allScopes, ICurrentOrganizationContext context,
        ICustomerContactStore store, CancellationToken cancellationToken)
    {
        if (!await store.CustomerExistsAsync(context.OrganizationId, customerId, cancellationToken)) return Results.NotFound();
        if (allScopes is not true && locationId is { } id && !await store.LocationExistsAsync(context.OrganizationId, customerId, id, cancellationToken))
            return Results.NotFound();
        if (allScopes is true && locationId is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(locationId)] = ["Não combine o filtro de escopo com um local específico."] });
        return Results.Ok(await store.ListAsync(context.OrganizationId, customerId, locationId, allScopes is true, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid customerId, Guid id, ICurrentOrganizationContext context,
        ICustomerContactStore store, CancellationToken cancellationToken)
    {
        var contact = await store.FindAsync(context.OrganizationId, customerId, id, cancellationToken);
        return contact is null ? Results.NotFound() : Results.Ok(ToDto(contact));
    }

    private static async Task<IResult> CreateAsync(Guid customerId, ContactRequest request, ICurrentOrganizationContext context,
        ICustomerContactStore store, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!await store.CustomerExistsAsync(context.OrganizationId, customerId, cancellationToken)) return Results.NotFound();
        if (request.LocationId is { } locationId
            && !await store.LocationExistsAsync(context.OrganizationId, customerId, locationId, cancellationToken)) return Results.NotFound();
        var now = DateTimeOffset.UtcNow;
        var contact = new CustomerContact(Guid.NewGuid(), context.OrganizationId, customerId, request.LocationId, context.UserId,
            request.Name!, request.Role, request.Phone, request.Email, request.IsPrimary, now);
        await store.AddAsync(contact, cancellationToken);
        return Results.Created($"/api/customers/{customerId}/contacts/{contact.Id}", ToDto(contact));
    }

    private static async Task<IResult> UpdateAsync(Guid customerId, Guid id, ContactRequest request, ICurrentOrganizationContext context,
        ICustomerContactStore store, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var contact = await store.FindAsync(context.OrganizationId, customerId, id, cancellationToken);
        if (contact is null) return Results.NotFound();
        if (request.LocationId is { } locationId
            && !await store.LocationExistsAsync(context.OrganizationId, customerId, locationId, cancellationToken)) return Results.NotFound();
        if (contact.LocationId != request.LocationId) return Results.ValidationProblem(new Dictionary<string, string[]>
            { [nameof(request.LocationId)] = ["O local do contato não pode ser alterado após o cadastro."] });
        contact.Update(context.UserId, request.Name!, request.Role, request.Phone, request.Email, request.IsPrimary, DateTimeOffset.UtcNow);
        await store.UpdateAsync(contact, cancellationToken);
        return Results.Ok(ToDto(contact));
    }

    private static async Task<IResult> DeleteAsync(Guid customerId, Guid id, ICurrentOrganizationContext context,
        ICustomerContactStore store, CancellationToken cancellationToken)
    {
        var contact = await store.FindAsync(context.OrganizationId, customerId, id, cancellationToken);
        if (contact is null) return Results.NotFound();
        if (await store.IsResponsibleForLocationAsync(context.OrganizationId, customerId, id, cancellationToken))
            return Results.Conflict(new { message = "Este contato está definido como responsável por um local. Altere o responsável antes de excluí-lo." });
        await store.DeleteAsync(contact, cancellationToken);
        return Results.NoContent();
    }

    private static Dictionary<string, string[]> Validate(ContactRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors[nameof(request.Name)] = ["Informe o nome do contato."];
        else if (request.Name.Trim().Length > 200) errors[nameof(request.Name)] = ["O nome deve ter até 200 caracteres."];
        if (request.Role?.Trim().Length > 200) errors[nameof(request.Role)] = ["O cargo/função deve ter até 200 caracteres."];
        if (request.Phone?.Trim().Length > 40) errors[nameof(request.Phone)] = ["O telefone deve ter até 40 caracteres."];
        if (request.Email?.Trim().Length > 254 || (!string.IsNullOrWhiteSpace(request.Email) && !MailAddress.TryCreate(request.Email.Trim(), out _)))
            errors[nameof(request.Email)] = ["Informe um e-mail válido com até 254 caracteres."];
        return errors;
    }

    private static CustomerContactDto ToDto(CustomerContact value) => new(value.Id, value.CustomerId, value.LocationId,
        value.Name, value.Role, value.Phone, value.Email, value.IsPrimary, value.CreatedAtUtc, value.UpdatedAtUtc);

    public sealed record ContactRequest(Guid? LocationId, string? Name, string? Role, string? Phone, string? Email, bool IsPrimary);
}
