using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;

namespace CDC.Common.Health;

/// <summary>
/// Maps the liveness and readiness endpoints. Shared by CDC.Api and CDC.Web.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Maps <c>/health</c> (liveness) and <c>/health/ready</c> (key-gated readiness).</summary>
    /// <param name="app">The application to map the endpoints on.</param>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness only - process is responsive, no dependency calls, cheap. This is what an
        // ALB target-group/ECS container health check should point at; safe to leave reachable
        // since it reveals nothing and can't be abused to generate load.
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

        // Runs the registered health checks (e.g. database or downstream-dependency
        // connectivity - see the checks registered in Program.cs). Deliberately separate from
        // the liveness endpoint above, and gated behind ReadinessKeyFilter - never point an
        // ALB/ECS health check at this. Not only would a transient dependency blip take an
        // otherwise-healthy container out of rotation, but ALB health checks can't send the
        // required header anyway. For on-demand/manual diagnostics and internal monitoring only.
        app.MapGroup("/health/ready")
            .AddEndpointFilter<ReadinessKeyFilter>()
            .MapHealthChecks("", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });
    }
}
