using PitCrm.Application.Authorization;
using PitCrm.Application.Customers;
using PitCrm.Domain.Customers;

namespace PitCrm.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static void Map(WebApplication app)
    {
        var customers = app.MapGroup("/api/customers");
        customers.MapGet("", SearchAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadCustomersAndLocations);
        customers.MapGet("/{id:guid}", GetByIdAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadCustomersAndLocations);
        customers.MapPut("/{id:guid}", UpdateAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
        customers.MapPost("", CreateAsync)
            .AddEndpointFilter<Security.AntiforgeryEndpointFilter>()
            .RequireAuthorization(AuthorizationPolicies.ManageCustomersAndLocations);
    }

    private static async Task<IResult> SearchAsync(
        string? q,
        int? page,
        int? pageSize,
        ICurrentOrganizationContext organizationContext,
        ICustomerStore customerStore,
        CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1 || (long)(currentPage - 1) * currentPageSize > int.MaxValue)
        {
            errors["page"] = ["A página deve ser um número positivo dentro do limite permitido."];
        }
        if (currentPageSize < 1 || currentPageSize > 100)
        {
            errors["pageSize"] = ["O tamanho da página deve estar entre 1 e 100."];
        }
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await customerStore.SearchAsync(
            organizationContext.OrganizationId,
            q,
            currentPage,
            currentPageSize,
            cancellationToken);
        return Results.Ok(new CustomerPage(result.Items, currentPage, currentPageSize, result.TotalCount));
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        ICurrentOrganizationContext organizationContext,
        ICustomerStore customerStore,
        CancellationToken cancellationToken)
    {
        var customer = await customerStore.FindAsync(organizationContext.OrganizationId, id, cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(ToDto(customer));
    }

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        ICurrentOrganizationContext organizationContext,
        ICustomerStore customerStore,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var now = DateTimeOffset.UtcNow;
        var customer = new Customer(
            Guid.NewGuid(),
            organizationContext.OrganizationId,
            organizationContext.UserId,
            request.Name!,
            request.Document,
            request.Instructions,
            request.Address,
            request.Locality,
            request.City,
            request.State,
            now);
        await customerStore.AddAsync(customer, cancellationToken);

        return Results.Created($"/api/customers/{customer.Id}", ToDto(customer));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        ICurrentOrganizationContext organizationContext,
        ICustomerStore customerStore,
        CancellationToken cancellationToken)
    {
        var errors = Validate(new CreateCustomerRequest(request.Name, request.Document, request.Instructions,
            request.Address, request.Locality, request.City, request.State));
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var customer = await customerStore.FindForUpdateAsync(
            organizationContext.OrganizationId,
            id,
            cancellationToken);
        if (customer is null) return Results.NotFound();

        customer.UpdateDetails(
            organizationContext.UserId,
            request.Name!,
            request.Document,
            request.Instructions,
            request.Address,
            request.Locality,
            request.City,
            request.State,
            DateTimeOffset.UtcNow);
        await customerStore.UpdateAsync(customer, cancellationToken);
        return Results.Ok(ToDto(customer));
    }

    private static Dictionary<string, string[]> Validate(CreateCustomerRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(request.Name)] = ["Informe o nome do cliente."];
        }
        else if (request.Name.Trim().Length > 200)
        {
            errors[nameof(request.Name)] = ["O nome do cliente deve ter até 200 caracteres."];
        }
        if (request.Document?.Trim().Length > 30)
        {
            errors[nameof(request.Document)] = ["O documento deve ter até 30 caracteres."];
        }
        if (request.Instructions?.Trim().Length > 2000)
        {
            errors[nameof(request.Instructions)] = ["As instruções devem ter até 2000 caracteres."];
        }
        if (request.Address?.Trim().Length > 500) errors[nameof(request.Address)] = ["O endereço principal deve ter até 500 caracteres."];
        if (request.Locality?.Trim().Length > 200) errors[nameof(request.Locality)] = ["A localidade deve ter até 200 caracteres."];
        if (request.City?.Trim().Length > 200) errors[nameof(request.City)] = ["A cidade deve ter até 200 caracteres."];
        if (request.State?.Trim().Length > 200) errors[nameof(request.State)] = ["O estado deve ter até 200 caracteres."];

        return errors;
    }

    private static CustomerDto ToDto(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Document,
        customer.Instructions,
        customer.Address,
        customer.Locality,
        customer.City,
        customer.State,
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc);

    public sealed record CreateCustomerRequest(string? Name, string? Document, string? Instructions,
        string? Address, string? Locality, string? City, string? State);
    public sealed record UpdateCustomerRequest(string? Name, string? Document, string? Instructions,
        string? Address, string? Locality, string? City, string? State);
}
