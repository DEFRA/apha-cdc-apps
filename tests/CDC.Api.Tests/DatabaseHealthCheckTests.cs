using System.Data;
using CDC.Api.Features.Health;
using CDC.Api.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CDC.Api.Tests;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenConnectionFactoryThrows()
    {
        var healthCheck = new DatabaseHealthCheck(new FakeConnectionFactory(() =>
            throw new InvalidOperationException("ConnectionStrings:Default is not configured")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenDatabaseUnreachable()
    {
        var healthCheck = new DatabaseHealthCheck(new FakeConnectionFactory(() =>
            new SqlConnection("Server=invalid-host-for-tests;Connect Timeout=1;TrustServerCertificate=True;")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private sealed class FakeConnectionFactory(Func<IDbConnection> factory) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => factory();
    }
}
