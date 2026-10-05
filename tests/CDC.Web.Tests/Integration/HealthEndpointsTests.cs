using System.Net;
using System.Net.Http.Json;
using CDC.Common.Health;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.Configuration;

namespace CDC.Web.Tests.Integration;

// AddCidmAuthentication() requires Cidm config to be present for host startup validation, so this
// uses CdcWebTestFactory (not the plain WebApplicationFactory<Program>) even though these tests are
// unrelated to auth.
public class HealthEndpointsTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public HealthEndpointsTests(CdcWebTestFactory factory)
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
    public async Task HealthReady_PassesThroughToHealthChecks_WhenKeyCorrect()
    {
        // Only asserts the filter let the request reach the health check pipeline (not 404) and
        // that a real report came back - the overall status also reflects ApiConnectivityHealthCheck,
        // which has no live CDC.Api to reach in this test host, so it must not be asserted here.
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("HealthCheck:ReadinessKey", "test-readiness-key")
            ])));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReadinessKeyFilter.HeaderName, "test-readiness-key");

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(body!.Status));
    }

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
    }
}
