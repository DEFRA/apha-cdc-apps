using System.Diagnostics.CodeAnalysis;
using CDC.Api.Infrastructure;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CDC.Api.Features.Health;

/// <summary>
/// Confirms the DB connection factory can actually open a connection to SQL
/// Server. Exposed separately from the liveness endpoint ("/health") so
/// infra can choose whether/how to use it - an ALB or ECS health check
/// pointed at this could take a healthy app container out of rotation
/// during a transient DB blip.
/// </summary>
public sealed class DatabaseHealthCheck(IDbConnectionFactory connectionFactory) : IHealthCheck
{
    /// <summary>Reports whether a connection to the database can be opened.</summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>Healthy when the probe succeeds; otherwise unhealthy.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unable to connect to the database", ex);
        }
    }

    [ExcludeFromCodeCoverage(Justification =
        "Requires a live SQL Server; the exception handling around this is unit-tested, but this actual " +
        "I/O (including the connection-string validation inside CreateConnection) is only ever validated " +
        "against a real database via the /health/ready endpoint.")]
    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.QuerySingleAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
    }
}
