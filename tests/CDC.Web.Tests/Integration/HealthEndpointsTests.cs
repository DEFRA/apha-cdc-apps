using System.Net;
using System.Net.Http.Json;
using CDC.Common.Health;
using CDC.Web.Tests.TestSupport;

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

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
    }
}
