using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PitCrm.Api.Tests;

public sealed class ApiHealthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_endpoint_reports_the_api_as_available()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");
        var result = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", result?.Status);
    }

    private sealed record HealthResponse(string Status);
}
