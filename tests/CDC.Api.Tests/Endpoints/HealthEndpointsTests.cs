using System.Net;
using System.Net.Http.Json;
using CDC.Api.Features.Health;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__User", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__Password", "cdc-tests-password");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        _factory = factory;
    }

    [Fact]
    public async Task Root_ReturnsHelloWorld()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync());
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
        Assert.True(body.UptimeSeconds >= 0);
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
    public async Task HealthReady_ProbesDatabase_WhenKeyCorrect()
    {
        // No real SQL Server is available in this test environment, so this
        // only asserts the key check let the request through to the DB probe
        // (503 Unhealthy), not that connectivity succeeds - see
        // DatabaseHealthCheckTests for the health-check logic itself.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "local-dev-readiness-key");

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
        public double UptimeSeconds { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
