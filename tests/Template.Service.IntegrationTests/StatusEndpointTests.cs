namespace Arkanis.Template.Service.IntegrationTests;

using System.Net.Http.Json;
using Arkanis.Template.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Verifies the public service status endpoint.
/// </summary>
/// <param name="factory">The service test host factory.</param>
public sealed class StatusEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// Verifies that the service reports a healthy public status.
    /// </summary>
    [Fact]
    public async Task Status_endpoint_returns_healthy_status()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/status", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<ServiceStatusResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(status);
        Assert.Equal("Healthy", status.Status);
    }
}
