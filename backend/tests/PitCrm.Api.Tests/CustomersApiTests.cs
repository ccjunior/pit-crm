using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PitCrm.Application.Authorization;
using PitCrm.Application.Demands;
using PitCrm.Domain.Demands;
using PitCrm.Domain.Organizations;
using PitCrm.Infrastructure.Identity;
using PitCrm.Infrastructure.Persistence;

namespace PitCrm.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class CustomersApiTests
{
    [Fact]
    public async Task Customer_endpoints_validate_access_and_isolate_search_detail_and_creation()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("PITCRM_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString),
            "Set PITCRM_TEST_CONNECTION to a PostgreSQL test database before running integration tests.");

        var schemaName = $"customers_{Guid.NewGuid():N}";
        var rootConnection = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = "public" };
        var scopedConnection = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schemaName };
        var previousConnectionString = Environment.GetEnvironmentVariable("PITCRM_CONNECTION_STRING");

        await using (var connection = new NpgsqlConnection(rootConnection.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            Environment.SetEnvironmentVariable("PITCRM_CONNECTION_STRING", scopedConnection.ConnectionString);
            using var factory = new WebApplicationFactory<Program>();
            var organizationA = Guid.NewGuid();
            var organizationB = Guid.NewGuid();
            var password = $"Customer-{Guid.NewGuid():N}-Ee5!";
            var users = new Dictionary<string, string>();

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                await dbContext.Database.MigrateAsync();
                dbContext.Organizations.AddRange(
                    new Organization(organizationA, "Empresa A"),
                    new Organization(organizationB, "Empresa B"));
                await dbContext.SaveChangesAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                foreach (var roleName in new[] { RoleNames.Administrator, RoleNames.Manager, RoleNames.Operator })
                {
                    Assert.True((await roleManager.CreateAsync(
                        new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() })).Succeeded);
                }

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                await AddUserAsync(userManager, users, "A", organizationA, RoleNames.Administrator, password);
                await AddUserAsync(userManager, users, "A", organizationA, RoleNames.Manager, password);
                await AddUserAsync(userManager, users, "A", organizationA, RoleNames.Operator, password);
                await AddUserAsync(userManager, users, "B", organizationB, RoleNames.Administrator, password);
            }

            using var adminClient = await CreateAuthenticatedClientAsync(factory, users["A-" + RoleNames.Administrator], password);
            using var managerClient = await CreateAuthenticatedClientAsync(factory, users["A-" + RoleNames.Manager], password);
            using var operatorClient = await CreateAuthenticatedClientAsync(factory, users["A-" + RoleNames.Operator], password);
            using var foreignClient = await CreateAuthenticatedClientAsync(factory, users["B-" + RoleNames.Administrator], password);
            using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymousClient.GetAsync("/api/customers")).StatusCode);

            var missingCsrf = await adminClient.PostAsJsonAsync("/api/customers", new { name = "Sem token" });
            Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

            var invalid = await PostJsonAsync(adminClient, "/api/customers", new
            {
                name = "   ",
                document = new string('x', 31),
                instructions = new string('i', 2001)
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var invalidBody = await invalid.Content.ReadAsStringAsync();
            Assert.Contains("Name", invalidBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Document", invalidBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Instructions", invalidBody, StringComparison.OrdinalIgnoreCase);

            var createdResponse = await PostJsonAsync(adminClient, "/api/customers", new
            {
                name = "Cliente Alfa",
                document = "DOC-12345",
                instructions = "Receber na portaria",
                organizationId = organizationB,
                createdByUserId = Guid.NewGuid()
            });
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var created = await createdResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(created);
            Assert.Equal("Cliente Alfa", created.Name);
            Assert.Equal("DOC-12345", created.Document);
            Assert.Equal("Receber na portaria", created.Instructions);
            Assert.NotEqual(Guid.Empty, created.Id);

            var missingAddress = await PostJsonAsync(adminClient, $"/api/customers/{created.Id}/locations", new
            {
                name = "Unidade Central"
            });
            Assert.Equal(HttpStatusCode.BadRequest, missingAddress.StatusCode);
            Assert.Contains("Address", await missingAddress.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var locationCreate = await PostJsonAsync(adminClient, $"/api/customers/{created.Id}/locations", new
            {
                name = "Unidade Central",
                address = "Rua das Flores, 100",
                organizationId = organizationB,
                instructions = (string?)null
            });
            Assert.Equal(HttpStatusCode.Created, locationCreate.StatusCode);
            var location = await locationCreate.Content.ReadFromJsonAsync<LocationResponse>();
            Assert.NotNull(location);
            Assert.Equal(created.Id, location.CustomerId);
            Assert.Equal("Rua das Flores, 100", location.Address);
            Assert.Null(location.Instructions);

            var updatedLocationResponse = await PutJsonAsync(managerClient,
                $"/api/customers/{created.Id}/locations/{location.Id}", new
                {
                    name = "Unidade Central",
                    address = "Rua das Flores, 200",
                    instructions = "Acesso pela recepção"
                });
            Assert.Equal(HttpStatusCode.OK, updatedLocationResponse.StatusCode);
            var updatedLocation = await updatedLocationResponse.Content.ReadFromJsonAsync<LocationResponse>();
            Assert.NotNull(updatedLocation);
            Assert.Equal(location.Id, updatedLocation.Id);
            Assert.Equal("Rua das Flores, 200", updatedLocation.Address);
            Assert.Equal("Acesso pela recepção", updatedLocation.Instructions);

            Assert.Equal(HttpStatusCode.Forbidden,
                (await PostJsonAsync(operatorClient, $"/api/customers/{created.Id}/locations", new
                {
                    name = "Local bloqueado", address = "Rua 1"
                })).StatusCode);
            var locations = await adminClient.GetFromJsonAsync<LocationResponse[]>(
                $"/api/customers/{created.Id}/locations");
            Assert.NotNull(locations);
            Assert.Single(locations);
            Assert.Equal(location.Id, locations[0].Id);
            Assert.Equal(HttpStatusCode.NotFound,
                (await foreignClient.GetAsync($"/api/customers/{created.Id}/locations")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await adminClient.GetAsync($"/api/customers/{Guid.NewGuid()}/locations/{location.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await adminClient.GetAsync($"/api/customers/{Guid.NewGuid()}/locations")).StatusCode);

            var demandResponse = await PostJsonAsync(operatorClient, "/api/demands", new
            {
                customerId = created.Id,
                locationId = location.Id,
                description = "Solicitação de manutenção da unidade",
                origin = "whatsapp",
                priority = "média"
            });
            Assert.Equal(HttpStatusCode.Created, demandResponse.StatusCode);
            var demand = await demandResponse.Content.ReadFromJsonAsync<DemandResponse>();
            Assert.NotNull(demand);
            Assert.NotEqual(Guid.Empty, demand.Id);
            Assert.Equal(created.Id, demand.CustomerId);
            Assert.Equal(location.Id, demand.LocationId);
            Assert.Equal("WhatsApp", demand.Origin);
            Assert.Equal("Média", demand.Priority);
            Assert.Equal("Recebida", demand.Status);
            Assert.Null(demand.AssignedToUserId);
            Assert.Null(demand.TicketReference);
            Assert.Null(demand.PurchaseOrderReference);

            foreach (var client in new[] { adminClient, managerClient })
            {
                var duplicateReference = await PostJsonAsync(client, "/api/demands", new
                {
                    customerId = created.Id,
                    locationId = location.Id,
                    description = "Outra solicitação independente",
                    origin = "Outra",
                    ticketReference = "TICKET-COMPARTILHADO",
                    purchaseOrderReference = "OC-COMPARTILHADA"
                });
                Assert.Equal(HttpStatusCode.Created, duplicateReference.StatusCode);
                var duplicateDemand = await duplicateReference.Content.ReadFromJsonAsync<DemandResponse>();
                Assert.NotNull(duplicateDemand);
                Assert.NotEqual(demand.Id, duplicateDemand.Id);
                Assert.Equal("TICKET-COMPARTILHADO", duplicateDemand.TicketReference);
            }

            var secondCustomerResponse = await PostJsonAsync(adminClient, "/api/customers", new { name = "Cliente Delta" });
            var secondCustomer = await secondCustomerResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(secondCustomer);
            var mismatchedLocation = await PostJsonAsync(managerClient, "/api/demands", new
            {
                customerId = secondCustomer.Id,
                locationId = location.Id,
                description = "Local de outro cliente",
                origin = "Ligação"
            });
            Assert.Equal(HttpStatusCode.BadRequest, mismatchedLocation.StatusCode);
            Assert.Contains("LocationId", await mismatchedLocation.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var invalidDemand = await PostJsonAsync(adminClient, "/api/demands", new
            {
                customerId = Guid.Empty,
                locationId = Guid.Empty,
                description = new string('x', 4001),
                origin = "Origem inventada",
                priority = "Urgente",
                ticketReference = new string('T', 101)
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidDemand.StatusCode);
            var invalidDemandBody = await invalidDemand.Content.ReadAsStringAsync();
            Assert.Contains("CustomerId", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LocationId", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Description", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Origin", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Priority", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TicketReference", invalidDemandBody, StringComparison.OrdinalIgnoreCase);

            var otherOrganizationUserId = Guid.Empty;
            using (var scope = factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                otherOrganizationUserId = (await userManager.FindByEmailAsync(users["B-" + RoleNames.Administrator]))!.Id;
            }
            var foreignAssignee = await PostJsonAsync(adminClient, "/api/demands", new
            {
                customerId = created.Id,
                locationId = location.Id,
                description = "Responsável de outra organização",
                origin = "E-mail",
                assignedToUserId = otherOrganizationUserId
            });
            Assert.Equal(HttpStatusCode.BadRequest, foreignAssignee.StatusCode);
            Assert.Contains("AssignedToUserId", await foreignAssignee.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                Assert.Equal(1, await dbContext.DemandActivities.CountAsync(activity => activity.DemandId == demand.Id));
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var creator = await userManager.FindByEmailAsync(users["A-" + RoleNames.Administrator]);
                Assert.NotNull(creator);
                var failedDemandId = Guid.NewGuid();
                var failedDemand = new Demand(failedDemandId, organizationA, created.Id, location.Id, creator.Id,
                    null, "Falha transacional", "Outra", null, null, null, DateTimeOffset.UtcNow);
                var invalidActivity = new DemandActivity(Guid.NewGuid(), organizationA, failedDemandId, Guid.NewGuid(),
                    "DemandCreated", "Demanda criada.", DateTimeOffset.UtcNow);
                var demandStore = scope.ServiceProvider.GetRequiredService<IDemandStore>();
                await Assert.ThrowsAsync<DbUpdateException>(() => demandStore.CreateAsync(failedDemand, invalidActivity, CancellationToken.None));
                Assert.False(await dbContext.Demands.AsNoTracking().AnyAsync(item => item.Id == failedDemandId));
            }

            var updatedResponse = await PutJsonAsync(adminClient, $"/api/customers/{created.Id}", new
            {
                name = "Cliente Alfa Atualizado",
                document = "DOC-12345",
                instructions = "Entregar no balcão",
                organizationId = organizationB
            });
            Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
            var updated = await updatedResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(updated);
            Assert.Equal(created.Id, updated.Id);
            Assert.Equal("Cliente Alfa Atualizado", updated.Name);
            Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
            Assert.True(updated.UpdatedAtUtc >= updated.CreatedAtUtc);

            Assert.Equal(HttpStatusCode.Forbidden,
                (await PutJsonAsync(operatorClient, $"/api/customers/{created.Id}", new { name = "Alteração negada" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await PutJsonAsync(foreignClient, $"/api/customers/{created.Id}", new { name = "Fora do escopo" })).StatusCode);

            var optionalResponse = await PostJsonAsync(adminClient, "/api/customers", new { name = "Cliente Beta" });
            Assert.Equal(HttpStatusCode.Created, optionalResponse.StatusCode);
            var optionalCustomer = await optionalResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(optionalCustomer);
            Assert.Null(optionalCustomer.Document);
            Assert.Null(optionalCustomer.Instructions);

            var managerCreate = await PostJsonAsync(managerClient, "/api/customers", new { name = "Cliente Gama" });
            Assert.Equal(HttpStatusCode.Created, managerCreate.StatusCode);
            var operatorCreate = await PostJsonAsync(operatorClient, "/api/customers", new { name = "Cliente Bloqueado" });
            Assert.Equal(HttpStatusCode.Forbidden, operatorCreate.StatusCode);

            var search = await adminClient.GetFromJsonAsync<CustomerPageResponse>(
                "/api/customers?q=DOC-123&page=1&pageSize=1");
            Assert.NotNull(search);
            Assert.Equal(1, search.TotalCount);
            Assert.Single(search.Items);
            Assert.Equal("Cliente Alfa Atualizado", search.Items[0].Name);
            var wildcardSearch = await adminClient.GetFromJsonAsync<CustomerPageResponse>("/api/customers?q=%25");
            Assert.NotNull(wildcardSearch);
            Assert.Equal(0, wildcardSearch.TotalCount);

            var page = await adminClient.GetFromJsonAsync<CustomerPageResponse>("/api/customers?page=1&pageSize=2");
            Assert.NotNull(page);
            Assert.Equal(4, page.TotalCount);
            Assert.Equal(2, page.Items.Length);
            Assert.Equal(1, page.Page);
            Assert.Equal(2, page.PageSize);

            var detail = await adminClient.GetFromJsonAsync<CustomerResponse>($"/api/customers/{created.Id}");
            Assert.NotNull(detail);
            Assert.Equal(created.Id, detail.Id);

            Assert.Equal(HttpStatusCode.NotFound,
                (await foreignClient.GetAsync($"/api/customers/{created.Id}")).StatusCode);
            var foreignList = await foreignClient.GetFromJsonAsync<CustomerPageResponse>("/api/customers");
            Assert.NotNull(foreignList);
            Assert.Empty(foreignList.Items);
            Assert.Equal(0, foreignList.TotalCount);

            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/customers?page=0&pageSize=20")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/customers?page=1&pageSize=101")).StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PITCRM_CONNECTION_STRING", previousConnectionString);
            await using var connection = new NpgsqlConnection(rootConnection.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task AddUserAsync(
        UserManager<ApplicationUser> userManager,
        IDictionary<string, string> users,
        string organizationKey,
        Guid organizationId,
        string roleName,
        string password)
    {
        var email = $"{roleName}-{organizationId:N}@example.invalid";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = roleName,
            OrganizationId = organizationId,
            IsActive = true
        };
        Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, roleName)).Succeeded);
        users[$"{organizationKey}-{roleName}"] = email;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var token = await GetCsrfTokenAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        login.Headers.Add("X-CSRF-TOKEN", token);
        var response = await client.SendAsync(login);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            client.Dispose();
            throw new InvalidOperationException("Test user could not authenticate.");
        }

        return client;
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        Assert.NotNull(response?.RequestToken);
        return response.RequestToken;
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object body)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PutJsonAsync(HttpClient client, string path, object body)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);
    private sealed record CustomerResponse(
        Guid Id, string Name, string? Document, string? Instructions, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record CustomerListItem(Guid Id, string Name, string? Document, string? Instructions,
        DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record CustomerPageResponse(CustomerListItem[] Items, int Page, int PageSize, int TotalCount);
    private sealed record LocationResponse(Guid Id, Guid CustomerId, string Name, string Address, string? Instructions,
        DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record DemandResponse(Guid Id, Guid CustomerId, Guid LocationId, string Description, string Origin,
        string? Priority, string Status, string? TicketReference, string? PurchaseOrderReference,
        Guid? AssignedToUserId, DateTimeOffset CreatedAtUtc);
}
