using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CDC.Common.Health;

/// <summary>
/// MapHealthChecks' default response writer emits only the overall status as plain text
/// (e.g. "Degraded") - useless for a monitoring tool that needs to know *why*. This writes the
/// full report as JSON instead: which named check ran, its own status, and its
/// description/exception. Shared by CDC.Api and CDC.Web.
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>Writes the full health report as JSON.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="report">The health report to serialise.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                exception = entry.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload), context.RequestAborted);
    }
}
