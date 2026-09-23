// Serilog's process-wide bootstrap logger cannot initialize multiple WebApplicationFactory hosts concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Arkanis.Template.Service.IntegrationTests;

using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Verifies the Common health endpoints.
/// </summary>
/// <param name="factory">The service test host factory.</param>
public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// Verifies that a Common health endpoint returns success.
    /// </summary>
    /// <param name="path">The health endpoint path.</param>
    [Theory]
    [InlineData("/healthz/alive")]
    [InlineData("/healthz/ready")]
    [InlineData("/healthz/startup")]
    public async Task Health_endpoint_returns_success(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
