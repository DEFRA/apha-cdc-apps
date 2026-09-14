using CDC.Web.Features.Health;
using CDC.Web.Infrastructure;
using CDC.Web.Tests.Features.Landing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CDC.Web.Tests.Features.Health;

public class ApiConnectivityHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenApiReportsHealthy()
    {
        var healthCheck = new ApiConnectivityHealthCheck(
            new FakeApiClient(new ApiHealthResponse("Healthy", 1, DateTime.UtcNow)));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenApiReportsNonHealthyStatus()
    {
        var healthCheck = new ApiConnectivityHealthCheck(
            new FakeApiClient(new ApiHealthResponse("Unhealthy", 1, DateTime.UtcNow)));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenApiIsUnreachable()
    {
        var healthCheck = new ApiConnectivityHealthCheck(
            new FakeApiClient(throwOnGetHealth: new HttpRequestException("connection refused")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
    }
}
