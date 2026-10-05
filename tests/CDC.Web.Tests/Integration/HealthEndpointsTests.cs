using System.Net;
using System.Net.Http.Json;
using CDC.Common.Health;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CDC.Web.Tests.Integration;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
    }

    [Fact]
    public async Task HealthReady_ReturnsNotFound_WhenKeyMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsNotFound_WhenKeyWrong()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "wrong-key");

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthyStatus_WhenKeyCorrect()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("HealthCheck:ReadinessKey", "test-readiness-key")
            ])));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "test-readiness-key");

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body!.Status);
    }

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
    }
}
