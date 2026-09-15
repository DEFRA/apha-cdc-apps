using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace CDC.Web.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness only - process is responsive, no dependency calls, cheap.
        // This is what an ALB target-group/ECS container health check
        // should point at. Deliberately never calls CDC.Api: a slow/failing
        // dependency call here could make this endpoint itself time out,
        // which would get a perfectly healthy Web container killed over an
        // Api problem - the opposite of what a liveness check is for.
        app.MapGet("/health", () =>
        {
            var uptimeSeconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
            return Results.Json(new
            {
                status = "Healthy",
                uptimeSeconds = Math.Round(uptimeSeconds, 0),
                timestampUtc = DateTime.UtcNow
            });
        });

        // Checks whether CDC.Api is reachable (reports Degraded, not
        // Unhealthy - see ApiConnectivityHealthCheck). Gated behind
        // ReadinessKeyFilter and deliberately separate from the liveness
        // endpoint above - never point an ALB/ECS health check at this. For
        // on-demand/manual diagnostics and internal monitoring only.
        app.MapGroup("/health/ready")
            .AddEndpointFilter<ReadinessKeyFilter>()
            .MapHealthChecks("", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });
    }
}
