using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PitCrm.Application.Authorization;
using PitCrm.Application.Demands;
using PitCrm.Application.Proposals;
using PitCrm.Domain.Proposals;
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
                foreach (var roleName in RoleNames.All)
                    Assert.True(await roleManager.RoleExistsAsync(roleName));

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
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymousClient.GetAsync("/api/demands")).StatusCode);

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
                document = "529.982.247-25",
                instructions = "Receber na portaria",
                organizationId = organizationB,
                createdByUserId = Guid.NewGuid()
            });
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var created = await createdResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(created);
            Assert.Equal("Cliente Alfa", created.Name);
            Assert.Equal("52998224725", created.Document);
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

            var foreignCustomerResponse = await PostJsonAsync(foreignClient, "/api/customers", new { name = "Cliente Empresa B" });
            Assert.Equal(HttpStatusCode.Created, foreignCustomerResponse.StatusCode);
            var foreignCustomer = await foreignCustomerResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(foreignCustomer);
            var foreignLocationResponse = await PostJsonAsync(foreignClient, $"/api/customers/{foreignCustomer.Id}/locations", new
            {
                name = "Unidade B", address = "Rua Um, 1"
            });
            Assert.Equal(HttpStatusCode.Created, foreignLocationResponse.StatusCode);
            var foreignLocation = await foreignLocationResponse.Content.ReadFromJsonAsync<LocationResponse>();
            Assert.NotNull(foreignLocation);
            var foreignDemandResponse = await PostJsonAsync(foreignClient, "/api/demands", new
            {
                customerId = foreignCustomer.Id, locationId = foreignLocation.Id,
                description = "Primeira demanda da Empresa B", origin = "Outra"
            });
            Assert.Equal(HttpStatusCode.Created, foreignDemandResponse.StatusCode);
            var foreignDemand = await foreignDemandResponse.Content.ReadFromJsonAsync<DemandResponse>();
            Assert.NotNull(foreignDemand);
            Assert.Equal(1, foreignDemand.ReferenceNumber);

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
            Assert.Equal(1, demand.ReferenceNumber);
            Assert.Equal(1, demand.Version);
            Assert.Equal(created.Id, demand.CustomerId);
            Assert.Equal(location.Id, demand.LocationId);
            Assert.Equal("WhatsApp", demand.Origin);
            Assert.Equal("Média", demand.Priority);
            Assert.Equal("Recebida", demand.Status);
            Assert.Null(demand.AssignedToUserId);
            Assert.Empty(demand.ExternalReferences);

            Guid operatorUserId;
            Guid foreignAdminUserId;
            using (var scope = factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var operatorUser = await userManager.FindByEmailAsync(users["A-" + RoleNames.Operator]);
                Assert.NotNull(operatorUser);
                operatorUserId = operatorUser.Id;
                var foreignAdmin = await userManager.FindByEmailAsync(users["B-" + RoleNames.Administrator]);
                Assert.NotNull(foreignAdmin);
                foreignAdminUserId = foreignAdmin.Id;
            }

            var assignedDemandResponse = await PostJsonAsync(operatorClient, "/api/demands", new
            {
                customerId = created.Id,
                locationId = location.Id,
                description = "Solicitação atribuída para teste",
                origin = "Ligação",
                assignedToUserId = operatorUserId
            });
            Assert.Equal(HttpStatusCode.Created, assignedDemandResponse.StatusCode);
            var assignedDemand = await assignedDemandResponse.Content.ReadFromJsonAsync<DemandResponse>();
            Assert.NotNull(assignedDemand);
            Assert.Equal(2, assignedDemand.ReferenceNumber);

            var expectedReferenceNumber = 3;
            foreach (var client in new[] { adminClient, managerClient })
            {
                var duplicateReference = await PostJsonAsync(client, "/api/demands", new
                {
                    customerId = created.Id,
                    locationId = location.Id,
                    description = "Outra solicitação independente",
                    origin = "Outra",
                    externalReferences = new[]
                    {
                        new { type = "Chamado/OS", value = "TICKET-COMPARTILHADO" },
                        new { type = "Ordem de Compra", value = "OC-COMPARTILHADA" },
                        new { type = "Chamado/OS", value = "TICKET-SECUNDARIO" }
                    }
                });
                Assert.Equal(HttpStatusCode.Created, duplicateReference.StatusCode);
                var duplicateDemand = await duplicateReference.Content.ReadFromJsonAsync<DemandResponse>();
                Assert.NotNull(duplicateDemand);
                Assert.NotEqual(demand.Id, duplicateDemand.Id);
                Assert.Equal(expectedReferenceNumber++, duplicateDemand.ReferenceNumber);
                Assert.Equal(3, duplicateDemand.ExternalReferences.Length);
                Assert.Contains(duplicateDemand.ExternalReferences, reference =>
                    reference.Type == "Chamado/OS" && reference.Value == "TICKET-COMPARTILHADO");
                Assert.Contains(duplicateDemand.ExternalReferences, reference =>
                    reference.Type == "Chamado/OS" && reference.Value == "TICKET-SECUNDARIO");
            }

            var searchById = await adminClient.GetFromJsonAsync<DemandPageResponse>(
                $"/api/demands?q={demand.Id}&page=1&pageSize=20");
            Assert.NotNull(searchById);
            Assert.Equal(1, searchById.TotalCount);
            Assert.Equal(demand.Id, Assert.Single(searchById.Items).Id);

            var searchByReference = await adminClient.GetFromJsonAsync<DemandPageResponse>(
                "/api/demands?q=TICKET-COMPARTILHADO");
            Assert.NotNull(searchByReference);
            Assert.Equal(2, searchByReference.TotalCount);
            Assert.Equal(2, searchByReference.Items.Length);

            var searchByReferenceType = await adminClient.GetFromJsonAsync<DemandPageResponse>("/api/demands?q=Ordem%20de%20Compra");
            Assert.NotNull(searchByReferenceType);
            Assert.Equal(2, searchByReferenceType.TotalCount);

            var searchByDescription = await adminClient.GetFromJsonAsync<DemandPageResponse>(
                "/api/demands?q=manutenção%20da%20unidade");
            Assert.NotNull(searchByDescription);
            Assert.Equal(1, searchByDescription.TotalCount);
            Assert.Equal(demand.Id, Assert.Single(searchByDescription.Items).Id);

            var searchByShortCode = await adminClient.GetFromJsonAsync<DemandPageResponse>("/api/demands?q=DEM-0001");
            Assert.NotNull(searchByShortCode);
            Assert.Equal(demand.Id, Assert.Single(searchByShortCode.Items).Id);

            var filteredPage = await adminClient.GetFromJsonAsync<DemandPageResponse>(
                $"/api/demands?customerId={created.Id}&locationId={location.Id}&assignedToUserId={operatorUserId}&page=1&pageSize=1");
            Assert.NotNull(filteredPage);
            Assert.Equal(1, filteredPage.TotalCount);
            var filteredItem = Assert.Single(filteredPage.Items);
            Assert.Equal(assignedDemand.Id, filteredItem.Id);
            Assert.Equal("Operador", filteredItem.AssignedToDisplayName);

            var unassignedPage = await adminClient.GetFromJsonAsync<DemandPageResponse>(
                $"/api/demands?q={demand.Id}");
            Assert.NotNull(unassignedPage);
            Assert.Null(Assert.Single(unassignedPage.Items).AssignedToUserId);
            var unassignedDetail = await adminClient.GetFromJsonAsync<DemandDetailsResponse>(
                $"/api/demands/{demand.Id}");
            Assert.NotNull(unassignedDetail);
            Assert.Equal(demand.ReferenceNumber, unassignedDetail.ReferenceNumber);
            Assert.Equal("Cliente Alfa", unassignedDetail.CustomerName);
            Assert.Equal("Unidade Central", unassignedDetail.LocationName);
            Assert.Null(unassignedDetail.AssignedToUserId);
            Assert.Null(unassignedDetail.AssignedToDisplayName);
            Assert.Equal("Operador", unassignedDetail.CreatedByDisplayName);
            Assert.Contains(unassignedDetail.Activities, activity => activity.EventType == "DemandCreated");

            var secondCustomerResponse = await PostJsonAsync(adminClient, "/api/customers", new { name = "Cliente Delta" });
            Assert.Equal(HttpStatusCode.Created, secondCustomerResponse.StatusCode);
            var secondCustomer = await secondCustomerResponse.Content.ReadFromJsonAsync<CustomerResponse>();
            Assert.NotNull(secondCustomer);
            var secondCustomerLocationResponse = await PostJsonAsync(adminClient,
                $"/api/customers/{secondCustomer.Id}/locations", new { name = "Unidade Delta", address = "Rua Delta, 1" });
            Assert.Equal(HttpStatusCode.Created, secondCustomerLocationResponse.StatusCode);
            var secondCustomerLocation = await secondCustomerLocationResponse.Content.ReadFromJsonAsync<LocationResponse>();
            Assert.NotNull(secondCustomerLocation);

            var firstUpdate = await PutJsonAsync(operatorClient, $"/api/demands/{demand.Id}", new
            {
                version = unassignedDetail.Version,
                customerId = demand.CustomerId,
                locationId = demand.LocationId,
                description = "Descrição atualizada pelo Operador",
                origin = "E-mail",
                priority = "Alta",
                externalReferences = new[]
                {
                    new { type = "Chamado/OS", value = "TICKET-ATUALIZADO" },
                    new { type = "Ordem de Compra", value = "OC-ATUALIZADA" },
                    new { type = "Código do Cliente", value = "REF-123" }
                },
                assignedToUserId = operatorUserId
            });
            Assert.True(firstUpdate.StatusCode == HttpStatusCode.OK,
                await firstUpdate.Content.ReadAsStringAsync());
            var firstUpdatedDemand = await firstUpdate.Content.ReadFromJsonAsync<DemandUpdateResponse>();
            Assert.NotNull(firstUpdatedDemand);
            Assert.Equal(demand.Id, firstUpdatedDemand.Id);
            Assert.Equal(demand.ReferenceNumber, firstUpdatedDemand.ReferenceNumber);
            Assert.Equal(2, firstUpdatedDemand.Version);
            Assert.Equal("Descrição atualizada pelo Operador", firstUpdatedDemand.Description);
            Assert.Equal("E-mail", firstUpdatedDemand.Origin);
            Assert.Equal("Alta", firstUpdatedDemand.Priority);
            Assert.Equal(3, firstUpdatedDemand.ExternalReferences.Length);
            Assert.Contains(firstUpdatedDemand.ExternalReferences, reference =>
                reference.Type == "Chamado/OS" && reference.Value == "TICKET-ATUALIZADO");
            Assert.Contains(firstUpdatedDemand.ExternalReferences, reference =>
                reference.Type == "Ordem de Compra" && reference.Value == "OC-ATUALIZADA");
            Assert.Contains(firstUpdatedDemand.ExternalReferences, reference =>
                reference.Type == "Código do Cliente" && reference.Value == "REF-123");
            Assert.Equal(operatorUserId, firstUpdatedDemand.AssignedToUserId);
            Assert.Equal("Recebida", firstUpdatedDemand.Status);

            var missingUpdateCsrf = await adminClient.PutAsJsonAsync($"/api/demands/{demand.Id}", new
            {
                version = firstUpdatedDemand.Version, customerId = demand.CustomerId,
                locationId = demand.LocationId, description = "Sem antifalsificação", origin = "Outra"
            });
            Assert.Equal(HttpStatusCode.BadRequest, missingUpdateCsrf.StatusCode);
            var invalidVersion = await PutJsonAsync(adminClient, $"/api/demands/{demand.Id}", new
            {
                version = 0, customerId = demand.CustomerId, locationId = demand.LocationId,
                description = "Versão inválida", origin = "Outra"
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidVersion.StatusCode);
            Assert.Contains("Version", await invalidVersion.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var staleUpdate = await PutJsonAsync(managerClient, $"/api/demands/{demand.Id}", new
            {
                version = unassignedDetail.Version,
                customerId = demand.CustomerId,
                locationId = demand.LocationId,
                description = "Essa gravação desatualizada não pode vencer",
                origin = "WhatsApp",
                priority = "Baixa",
                externalReferences = Array.Empty<object>(),
                assignedToUserId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
            Assert.Contains("Recarregue", await staleUpdate.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var concurrentVersion = firstUpdatedDemand.Version;
            var concurrentUpdates = await Task.WhenAll(
                PutJsonAsync(adminClient, $"/api/demands/{demand.Id}", new
                {
                    version = concurrentVersion,
                    customerId = demand.CustomerId,
                    locationId = demand.LocationId,
                    description = "Atualização concorrente Administrador",
                    origin = "WhatsApp",
                    priority = "Baixa",
                    externalReferences = new[] { new { type = "Chamado/OS", value = "TICKET-ADMIN" } },
                    assignedToUserId = operatorUserId
                }),
                PutJsonAsync(managerClient, $"/api/demands/{demand.Id}", new
                {
                    version = concurrentVersion,
                    customerId = demand.CustomerId,
                    locationId = demand.LocationId,
                    description = "Atualização concorrente Gestor",
                    origin = "Ligação",
                    priority = "Média",
                    externalReferences = new[] { new { type = "Chamado/OS", value = "TICKET-GESTOR" } },
                    assignedToUserId = (Guid?)null
                }));
            Assert.Single(concurrentUpdates, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(concurrentUpdates, response => response.StatusCode == HttpStatusCode.Conflict);
            foreach (var response in concurrentUpdates) response.Dispose();

            var afterConcurrency = await adminClient.GetFromJsonAsync<DemandDetailsResponse>($"/api/demands/{demand.Id}");
            Assert.NotNull(afterConcurrency);
            Assert.Equal(concurrentVersion + 1, afterConcurrency.Version);
            Assert.Contains(afterConcurrency.Description,
                new[] { "Atualização concorrente Administrador", "Atualização concorrente Gestor" });
            Assert.Equal(2, afterConcurrency.Activities.Count(activity => activity.EventType == "DemandUpdated"));
            var mismatchedCustomerLocation = await PutJsonAsync(adminClient, $"/api/demands/{demand.Id}", new
            {
                version = afterConcurrency.Version, customerId = secondCustomer.Id,
                locationId = location.Id, description = "Local de outro cliente da mesma organização",
                origin = "Outra", priority = (string?)null, externalReferences = Array.Empty<object>(),
                assignedToUserId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.BadRequest, mismatchedCustomerLocation.StatusCode);
            Assert.Contains("LocationId", await mismatchedCustomerLocation.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            var foreignCustomerUpdate = await PutJsonAsync(adminClient, $"/api/demands/{demand.Id}", new
            {
                version = afterConcurrency.Version, customerId = foreignCustomer.Id,
                locationId = foreignLocation.Id, description = "Cliente de outra organização",
                origin = "Outra", priority = (string?)null, externalReferences = Array.Empty<object>(),
                assignedToUserId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.BadRequest, foreignCustomerUpdate.StatusCode);
            Assert.Contains("CustomerId", await foreignCustomerUpdate.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            var foreignAssigneeUpdate = await PutJsonAsync(adminClient, $"/api/demands/{demand.Id}", new
            {
                version = afterConcurrency.Version, customerId = demand.CustomerId,
                locationId = demand.LocationId, description = "Responsável fora da organização",
                origin = "Outra", priority = (string?)null, externalReferences = Array.Empty<object>(),
                assignedToUserId = foreignAdminUserId
            });
            Assert.Equal(HttpStatusCode.BadRequest, foreignAssigneeUpdate.StatusCode);
            Assert.Contains("AssignedToUserId", await foreignAssigneeUpdate.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HttpStatusCode.NotFound,
                (await PutJsonAsync(foreignClient, $"/api/demands/{demand.Id}", new
                {
                    version = afterConcurrency.Version, customerId = foreignCustomer.Id,
                    locationId = foreignLocation.Id, description = "Demanda de outra organização",
                    origin = "Outra", priority = (string?)null, externalReferences = Array.Empty<object>(),
                    assignedToUserId = (Guid?)null
                })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await PutJsonAsync(anonymousClient, $"/api/demands/{demand.Id}", new
                {
                    version = afterConcurrency.Version, customerId = demand.CustomerId,
                    locationId = demand.LocationId, description = "Sem sessão",
                    origin = "Outra", priority = (string?)null, externalReferences = Array.Empty<object>(),
                    assignedToUserId = (Guid?)null
                })).StatusCode);

            var assignedDetail = await adminClient.GetFromJsonAsync<DemandDetailsResponse>(
                $"/api/demands/{assignedDemand.Id}");
            Assert.NotNull(assignedDetail);
            Assert.Equal(operatorUserId, assignedDetail.AssignedToUserId);
            Assert.Equal("Operador", assignedDetail.AssignedToDisplayName);
            Assert.Equal(HttpStatusCode.OK, (await managerClient.GetAsync($"/api/demands/{demand.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await operatorClient.GetAsync($"/api/demands/{demand.Id}")).StatusCode);

            var secondPage = await adminClient.GetFromJsonAsync<DemandPageResponse>(
                "/api/demands?page=2&pageSize=2");
            Assert.NotNull(secondPage);
            Assert.Equal(4, secondPage.TotalCount);
            Assert.Equal(2, secondPage.Items.Length);
            Assert.Equal(2, secondPage.Page);
            Assert.Equal(2, secondPage.PageSize);

            Assert.Equal(HttpStatusCode.NotFound,
                (await adminClient.GetAsync("/api/demands/TICKET-COMPARTILHADO")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await foreignClient.GetAsync($"/api/demands/{demand.Id}")).StatusCode);
            var foreignSearch = await foreignClient.GetFromJsonAsync<DemandPageResponse>(
                "/api/demands?q=TICKET-COMPARTILHADO");
            Assert.NotNull(foreignSearch);
            Assert.Empty(foreignSearch.Items);
            Assert.Equal(0, foreignSearch.TotalCount);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/demands?page=0&pageSize=20")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/demands?page=1&pageSize=101")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/demands?page=2147483647&pageSize=100")).StatusCode);

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
                externalReferences = new[] { new { type = "x", value = new string('T', 101) } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidDemand.StatusCode);
            var invalidDemandBody = await invalidDemand.Content.ReadAsStringAsync();
            Assert.Contains("CustomerId", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LocationId", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Description", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Origin", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Priority", invalidDemandBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ExternalReferences[0].Value", invalidDemandBody, StringComparison.OrdinalIgnoreCase);

            var foreignAssignee = await PostJsonAsync(adminClient, "/api/demands", new
            {
                customerId = created.Id,
                locationId = location.Id,
                description = "Responsável de outra organização",
                origin = "E-mail",
                assignedToUserId = foreignAdminUserId
            });
            Assert.Equal(HttpStatusCode.BadRequest, foreignAssignee.StatusCode);
            Assert.Contains("AssignedToUserId", await foreignAssignee.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                Assert.Equal(3, await dbContext.DemandActivities.CountAsync(activity => activity.DemandId == demand.Id));
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var creator = await userManager.FindByEmailAsync(users["A-" + RoleNames.Administrator]);
                Assert.NotNull(creator);
                var failedDemandId = Guid.NewGuid();
                var failedDemand = new Demand(failedDemandId, organizationA, created.Id, location.Id, creator.Id,
                    null, "Falha transacional", "Outra", null, null, DateTimeOffset.UtcNow);
                var invalidActivity = new DemandActivity(Guid.NewGuid(), organizationA, failedDemandId, Guid.NewGuid(),
                    "DemandCreated", "Demanda criada.", DateTimeOffset.UtcNow);
                var demandStore = scope.ServiceProvider.GetRequiredService<IDemandStore>();
                await Assert.ThrowsAsync<DbUpdateException>(() => demandStore.CreateAsync(failedDemand, invalidActivity, CancellationToken.None));
                Assert.False(await dbContext.Demands.AsNoTracking().AnyAsync(item => item.Id == failedDemandId));
            }

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var demandStore = scope.ServiceProvider.GetRequiredService<IDemandStore>();
                var persistedDemand = await dbContext.Demands.AsNoTracking().SingleAsync(item => item.Id == demand.Id);
                var invalidUpdateData = new DemandUpdateData(persistedDemand.Version, persistedDemand.CustomerId,
                    persistedDemand.LocationId, "Esta alteração deve ser revertida", persistedDemand.Origin,
                    persistedDemand.Priority, null, persistedDemand.AssignedToUserId);
                await Assert.ThrowsAsync<DbUpdateException>(() => demandStore.UpdateAsync(organizationA,
                    persistedDemand.Id, Guid.NewGuid(), invalidUpdateData, DateTimeOffset.UtcNow, CancellationToken.None));
            }

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PitCrmDbContext>();
                var persistedDemand = await dbContext.Demands.AsNoTracking().SingleAsync(item => item.Id == demand.Id);
                Assert.Equal(afterConcurrency.Version, persistedDemand.Version);
                Assert.Equal(afterConcurrency.Description, persistedDemand.Description);
                Assert.Equal(3, await dbContext.DemandActivities.CountAsync(activity => activity.DemandId == demand.Id));
            }

            var updatedResponse = await PutJsonAsync(adminClient, $"/api/customers/{created.Id}", new
            {
                name = "Cliente Alfa Atualizado",
                document = "529.982.247-25",
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
                "/api/customers?q=529.982&page=1&pageSize=1");
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
            var foreignListedCustomer = Assert.Single(foreignList.Items);
            Assert.Equal(1, foreignList.TotalCount);
            Assert.Equal("Cliente Empresa B", foreignListedCustomer.Name);

            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/customers?page=0&pageSize=20")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminClient.GetAsync("/api/customers?page=1&pageSize=101")).StatusCode);

            await VerifyCommercialConversionAsync(managerClient, operatorClient, foreignClient, created.Id, location.Id);
            await VerifyProspectRevisionAsync(managerClient, operatorClient, created.Id, location.Id);
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

    private static async Task VerifyCommercialConversionAsync(HttpClient manager, HttpClient operatorClient,
        HttpClient otherOrganization, Guid customerId, Guid locationId)
    {
        var managerSession = await manager.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var responsibleUserId = managerSession.GetProperty("id").GetGuid();
        var request = new
        {
            customerId,
            responsibleUserId,
            origin = "E-mail",
            description = "Reforma de teste",
            references = new[]
            {
                new { type = "RFQ/cotação", value = "RFQ-AC-T02" },
                new { type = "Pedido de compra", value = "OC-AC-T02" }
            }
        };
        using var denied = await PostJsonAsync(operatorClient, "/api/proposals", request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var crossTenant = await PostJsonAsync(otherOrganization, "/api/proposals", request);
        Assert.Equal(HttpStatusCode.BadRequest, crossTenant.StatusCode);

        using var created = await PostJsonAsync(manager, "/api/proposals", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var proposal = (await created.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        Assert.Matches("^SOL-[0-9]{4}-0001$", proposal.Number);
        var yearPrefix = proposal.Number[..9];
        Assert.Equal($"{yearPrefix}0001", proposal.Number);
        Assert.Equal("E-mail", proposal.Origin);
        Assert.Collection(proposal.References.OrderBy(value => value.Type),
            reference =>
            {
                Assert.Equal("Pedido de compra", reference.Type);
                Assert.Equal("OC-AC-T02", reference.Value);
            },
            reference =>
            {
                Assert.Equal("RFQ/cotação", reference.Type);
                Assert.Equal("RFQ-AC-T02", reference.Value);
            });
        var proposalPath = $"/api/proposals/{proposal.Id}";
        using var foreignRead = await otherOrganization.GetAsync(proposalPath);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        using var operatorRead = await operatorClient.GetAsync(proposalPath);
        Assert.Equal(HttpStatusCode.Forbidden, operatorRead.StatusCode);
        using var operatorList = await operatorClient.GetAsync("/api/proposals");
        Assert.Equal(HttpStatusCode.Forbidden, operatorList.StatusCode);
        var foreignList = await otherOrganization.GetFromJsonAsync<ProposalPageDto>("/api/proposals");
        Assert.DoesNotContain(foreignList!.Items, item => item.Id == proposal.Id);

        using var invalidReference = await PostJsonAsync(manager, "/api/proposals", new
        {
            customerId,
            responsibleUserId,
            origin = "E-mail",
            description = "Referência com tipo desconhecido",
            references = new[] { new { type = "Tipo livre", value = "REF-INVALIDA" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidReference.StatusCode);

        using var unassigned = await PostJsonAsync(manager, "/api/proposals", new
        {
            prospectName = "Prospect sem responsável",
            responsibleUserId = (Guid?)null,
            origin = "Telefone",
            description = "Atribuição deve anteceder avaliação"
        });
        unassigned.EnsureSuccessStatusCode();
        var unassignedProposal = (await unassigned.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        Assert.Equal($"{yearPrefix}0002", unassignedProposal.Number);
        using var evaluationWithoutAssignment = await PostJsonAsync(manager,
            $"/api/proposals/{unassignedProposal.Id}/status",
            new { unassignedProposal.Version, status = CommercialRequest.Evaluating });
        Assert.Equal(HttpStatusCode.BadRequest, evaluationWithoutAssignment.StatusCode);
        using var assigned = await PutJsonAsync(manager, $"/api/proposals/{unassignedProposal.Id}", new
        {
            unassignedProposal.Version,
            prospectName = "Prospect sem responsável",
            responsibleUserId,
            origin = "Telefone",
            description = "Atribuição deve anteceder avaliação",
            references = Array.Empty<object>()
        });
        assigned.EnsureSuccessStatusCode();
        unassignedProposal = (await assigned.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        Assert.Equal(responsibleUserId, unassignedProposal.ResponsibleUserId);
        using var evaluationAfterAssignment = await PostJsonAsync(manager,
            $"/api/proposals/{unassignedProposal.Id}/status",
            new { unassignedProposal.Version, status = CommercialRequest.Evaluating });
        evaluationAfterAssignment.EnsureSuccessStatusCode();

        using var otherOrganizationProposal = await PostJsonAsync(otherOrganization, "/api/proposals", new
        {
            prospectName = "Solicitação da Empresa B",
            origin = "WhatsApp",
            description = "Numeração isolada por organização"
        });
        otherOrganizationProposal.EnsureSuccessStatusCode();
        var foreignProposal = (await otherOrganizationProposal.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        Assert.Equal($"{yearPrefix}0001", foreignProposal.Number);

        var concurrentResponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            PostJsonAsync(manager, "/api/proposals", new
            {
                prospectName = $"Concorrente {index}",
                responsibleUserId,
                origin = "Indicação",
                description = "Geração concorrente de número comercial"
            })));
        try
        {
            var concurrentProposals = new List<ProposalDetailsDto>();
            foreach (var response in concurrentResponses)
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                concurrentProposals.Add((await response.Content.ReadFromJsonAsync<ProposalDetailsDto>())!);
            }

            var numbers = concurrentProposals.Select(value => value.Number).ToArray();
            Assert.Equal(numbers.Length, numbers.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(Enumerable.Range(3, 8).Select(value => $"{yearPrefix}{value:D4}").OrderBy(value => value),
                numbers.OrderBy(value => value));
        }
        finally
        {
            foreach (var response in concurrentResponses) response.Dispose();
        }
        using var foreignUpdate = await PutJsonAsync(otherOrganization, proposalPath,
            new { proposal.Version, customerId, origin = "E-mail", description = "Tentativa cruzada" });
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
        using var foreignStatus = await PostJsonAsync(otherOrganization, proposalPath + "/status",
            new { proposal.Version, status = CommercialRequest.Evaluating });
        Assert.Equal(HttpStatusCode.NotFound, foreignStatus.StatusCode);
        using var operatorStatus = await PostJsonAsync(operatorClient, proposalPath + "/status",
            new { proposal.Version, status = CommercialRequest.Evaluating });
        Assert.Equal(HttpStatusCode.Forbidden, operatorStatus.StatusCode);

        using var evaluated = await PostJsonAsync(manager, proposalPath + "/status",
            new { proposal.Version, status = CommercialRequest.Evaluating });
        evaluated.EnsureSuccessStatusCode();
        proposal = (await evaluated.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;

        using var foreignRevision = await PostJsonAsync(otherOrganization, proposalPath + "/revisions",
            new { proposal.Version, items = new[] { new { description = "Tentativa", amount = 1m } } });
        Assert.Equal(HttpStatusCode.NotFound, foreignRevision.StatusCode);
        using var revised = await PostJsonAsync(manager, proposalPath + "/revisions",
            new { proposal.Version, items = new[] { new { description = "Pintura", amount = 120.50m } } });
        revised.EnsureSuccessStatusCode();
        proposal = (await revised.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        var revision = Assert.Single(proposal.Revisions);
        var item = Assert.Single(revision.Items);

        var sentAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        using var foreignSend = await PostJsonAsync(otherOrganization, proposalPath + "/send",
            new { proposal.Version, revisionId = revision.Id, channel = "Email", sentAtUtc });
        Assert.Equal(HttpStatusCode.NotFound, foreignSend.StatusCode);
        using var sent = await PostJsonAsync(manager, proposalPath + "/send",
            new { proposal.Version, revisionId = revision.Id, channel = "Email", sentAtUtc });
        sent.EnsureSuccessStatusCode();
        proposal = (await sent.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        Assert.Equal("E-mail", proposal.Origin);
        Assert.Equal("Email", Assert.Single(proposal.Revisions).SendChannel);

        var approval = new { proposal.Version, revisionId = revision.Id, approverName = "Cliente Teste",
            channel = "Email", approvedAtUtc = sentAtUtc.AddMinutes(1) };
        var approvalPath = proposalPath + "/approve";
        using var foreignApproval = await PostJsonAsync(otherOrganization, approvalPath, approval);
        Assert.Equal(HttpStatusCode.NotFound, foreignApproval.StatusCode);
        using var deniedApproval = await PostJsonAsync(operatorClient, approvalPath, approval);
        Assert.Equal(HttpStatusCode.Forbidden, deniedApproval.StatusCode);

        var concurrent = await Task.WhenAll(PostJsonAsync(manager, approvalPath, approval),
            PostJsonAsync(manager, approvalPath, approval));
        using var first = concurrent[0];
        using var second = concurrent[1];
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var draftId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demandDraftId").GetGuid();
        Assert.Equal(draftId, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demandDraftId").GetGuid());
        using var replay = await PostJsonAsync(manager, approvalPath, approval);
        replay.EnsureSuccessStatusCode();
        Assert.Equal(draftId, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demandDraftId").GetGuid());

        var draftPath = $"/api/demand-drafts/{draftId}";
        using var foreignDraft = await otherOrganization.GetAsync(draftPath);
        Assert.Equal(HttpStatusCode.NotFound, foreignDraft.StatusCode);
        var foreignDrafts = await otherOrganization.GetFromJsonAsync<JsonElement>("/api/demand-drafts");
        Assert.DoesNotContain(foreignDrafts.GetProperty("items").EnumerateArray(),
            value => value.GetProperty("id").GetGuid() == draftId);
        var draft = await operatorClient.GetFromJsonAsync<JsonElement>(draftPath);
        using var foreignDraftUpdate = await PutJsonAsync(otherOrganization, draftPath,
            new { version = draft.GetProperty("version").GetInt32(), customerId, locationId,
                description = request.description, origin = request.origin });
        Assert.Equal(HttpStatusCode.NotFound, foreignDraftUpdate.StatusCode);
        using var foreignConvert = await PostJsonAsync(otherOrganization, draftPath + "/convert",
            new { version = draft.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.NotFound, foreignConvert.StatusCode);
        using var incomplete = await PostJsonAsync(operatorClient, draftPath + "/convert",
            new { version = draft.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        using var updated = await PutJsonAsync(operatorClient, draftPath,
            new { version = draft.GetProperty("version").GetInt32(), customerId, locationId,
                description = request.description, origin = request.origin });
        updated.EnsureSuccessStatusCode();
        draft = await updated.Content.ReadFromJsonAsync<JsonElement>();
        using var converted = await PostJsonAsync(operatorClient, draftPath + "/convert",
            new { version = draft.GetProperty("version").GetInt32() });
        converted.EnsureSuccessStatusCode();
        var demandId = (await converted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demandId").GetGuid();
        using var convertedAgain = await PostJsonAsync(operatorClient, draftPath + "/convert",
            new { version = draft.GetProperty("version").GetInt32() });
        convertedAgain.EnsureSuccessStatusCode();
        Assert.Equal(demandId, (await convertedAgain.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demandId").GetGuid());
        var demand = await operatorClient.GetFromJsonAsync<JsonElement>($"/api/demands/{demandId}");
        var subitem = Assert.Single(demand.GetProperty("subitems").EnumerateArray());
        Assert.Equal(item.Id, subitem.GetProperty("sourceProposalItemId").GetGuid());
        Assert.Equal(item.Description, subitem.GetProperty("description").GetString());
        Assert.Equal(item.Amount, subitem.GetProperty("amount").GetDecimal());
    }

    private static async Task VerifyProspectRevisionAsync(HttpClient manager, HttpClient operatorClient,
        Guid customerId, Guid locationId)
    {
        var managerSession = await manager.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var responsibleUserId = managerSession.GetProperty("id").GetGuid();
        using var created = await PostJsonAsync(manager, "/api/proposals",
            new { prospectName = "Prospect Teste", responsibleUserId, origin = "E-mail", description = "Reforma" });
        created.EnsureSuccessStatusCode();
        var proposal = (await created.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        var path = $"/api/proposals/{proposal.Id}";
        using var evaluated = await PostJsonAsync(manager, path + "/status",
            new { proposal.Version, status = CommercialRequest.Evaluating });
        evaluated.EnsureSuccessStatusCode();
        proposal = (await evaluated.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        using var firstRevision = await PostJsonAsync(manager, path + "/revisions",
            new { proposal.Version, items = new[] { new { description = "Serviço inicial", amount = 100m } } });
        firstRevision.EnsureSuccessStatusCode();
        proposal = (await firstRevision.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        var revision = Assert.Single(proposal.Revisions);
        var timestamp = DateTimeOffset.UtcNow;
        using var sent = await PostJsonAsync(manager, path + "/send",
            new { proposal.Version, revisionId = revision.Id, channel = "E-mail", sentAtUtc = timestamp });
        sent.EnsureSuccessStatusCode();
        proposal = (await sent.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        using var approved = await PostJsonAsync(manager, path + "/approve",
            new { proposal.Version, revisionId = revision.Id, approverName = "Cliente", channel = "E-mail", approvedAtUtc = timestamp });
        approved.EnsureSuccessStatusCode();
        var approval = await approved.Content.ReadFromJsonAsync<JsonElement>();
        var draftId = approval.GetProperty("demandDraftId").GetGuid();
        proposal = (await manager.GetFromJsonAsync<ProposalDetailsDto>(path))!;

        using var secondRevision = await PostJsonAsync(manager, path + "/revisions",
            new { proposal.Version, items = new[] { new { description = "Serviço revisado", amount = 150m } } });
        secondRevision.EnsureSuccessStatusCode();
        proposal = (await secondRevision.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        Assert.Equal(2, proposal.Revisions.Count);
        revision = proposal.Revisions.Single(value => value.Number == 2);
        using var secondSend = await PostJsonAsync(manager, path + "/send",
            new { proposal.Version, revisionId = revision.Id, channel = "E-mail", sentAtUtc = timestamp });
        secondSend.EnsureSuccessStatusCode();
        proposal = (await secondSend.Content.ReadFromJsonAsync<ProposalDetailsDto>())!;
        using var secondApproval = await PostJsonAsync(manager, path + "/approve",
            new { proposal.Version, revisionId = revision.Id, approverName = "Cliente", channel = "E-mail", approvedAtUtc = timestamp });
        secondApproval.EnsureSuccessStatusCode();
        Assert.Equal(draftId, (await secondApproval.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("demandDraftId").GetGuid());

        var draftPath = $"/api/demand-drafts/{draftId}";
        var draft = await operatorClient.GetFromJsonAsync<JsonElement>(draftPath);
        var item = Assert.Single(draft.GetProperty("items").EnumerateArray());
        Assert.Equal("Serviço revisado", item.GetProperty("description").GetString());
        using var denied = await PutJsonAsync(operatorClient, draftPath,
            new { version = draft.GetProperty("version").GetInt32(), customerId, locationId, description = "Reforma", origin = "E-mail" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var linked = await PutJsonAsync(manager, draftPath,
            new { version = draft.GetProperty("version").GetInt32(), customerId, locationId, description = "Reforma", origin = "E-mail" });
        linked.EnsureSuccessStatusCode();
        draft = await linked.Content.ReadFromJsonAsync<JsonElement>();
        using var converted = await PostJsonAsync(operatorClient, draftPath + "/convert",
            new { version = draft.GetProperty("version").GetInt32() });
        converted.EnsureSuccessStatusCode();
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
    private sealed record DemandResponse(Guid Id, int ReferenceNumber, int Version, Guid CustomerId, Guid LocationId, string Description, string Origin,
        string? Priority, string Status, DemandExternalReferenceResponse[] ExternalReferences,
        Guid? AssignedToUserId, DateTimeOffset CreatedAtUtc);
    private sealed record DemandUpdateResponse(Guid Id, int ReferenceNumber, int Version, Guid CustomerId,
        Guid LocationId, string Description, string Origin, string? Priority, string Status,
        DemandExternalReferenceResponse[] ExternalReferences, Guid? AssignedToUserId);
    private sealed record DemandPageResponse(DemandListItemResponse[] Items, int Page, int PageSize, int TotalCount);
    private sealed record DemandListItemResponse(Guid Id, int ReferenceNumber, Guid CustomerId, string CustomerName, Guid LocationId,
        string LocationName, string Description, string Origin, string? Priority, string Status,
        Guid? AssignedToUserId, string? AssignedToDisplayName, DateTimeOffset CreatedAtUtc);
    private sealed record DemandDetailsResponse(Guid Id, int ReferenceNumber, int Version, Guid CustomerId, string CustomerName, Guid LocationId,
        string LocationName, string Description, string Origin, string? Priority, string Status,
        DemandExternalReferenceResponse[] ExternalReferences, Guid? AssignedToUserId,
        string? AssignedToDisplayName, Guid CreatedByUserId, string CreatedByDisplayName,
        DateTimeOffset CreatedAtUtc, DemandActivityResponse[] Activities);
    private sealed record DemandExternalReferenceResponse(string Type, string Value);
    private sealed record DemandActivityResponse(Guid Id, Guid AuthorUserId, string AuthorDisplayName,
        string EventType, string Description, DateTimeOffset OccurredAtUtc);
}
