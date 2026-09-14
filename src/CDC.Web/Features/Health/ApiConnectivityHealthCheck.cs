using CDC.Web.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CDC.Web.Features.Health;

/// <summary>
/// Confirms CDC.Api is reachable. Reports Degraded, not Unhealthy, when it
/// isn't - CDC.Web can still serve everything that doesn't depend on Api
/// (static pages, any Api-independent feature), so this must never be
/// treated as Web itself being down. By default a Degraded result still
/// returns HTTP 200 from the health check middleware, so it never causes
/// an ALB/ECS health check to pull Web out of rotation - it's here purely
/// so a monitoring tool or dashboard reading /health/ready's JSON body can
/// see that the Api dependency is unhealthy.
/// </summary>
public sealed class ApiConnectivityHealthCheck(IApiClient apiClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await apiClient.GetHealthAsync(cancellationToken);
            return health?.Status == "Healthy"
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("CDC.Api responded but did not report Healthy status");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("CDC.Api is unreachable", ex);
        }
    }
}
