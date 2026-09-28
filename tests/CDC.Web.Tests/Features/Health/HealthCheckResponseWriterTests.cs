using System.Text.Json;
using CDC.Common.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CDC.Web.Tests.Features.Health;

public class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteResponse_WritesJsonWithStatusAndCheckDetails()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["api-connectivity"] = new HealthReportEntry(
                status: HealthStatus.Degraded,
                description: "CDC.Api is unreachable",
                duration: TimeSpan.FromMilliseconds(5),
                exception: new InvalidOperationException("boom"),
                data: null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        Assert.Equal("application/json", httpContext.Response.ContentType);
        httpContext.Response.Body.Position = 0;
        using var doc = JsonDocument.Parse(httpContext.Response.Body);
        var root = doc.RootElement;
        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        var check = root.GetProperty("checks")[0];
        Assert.Equal("api-connectivity", check.GetProperty("name").GetString());
        Assert.Equal("Degraded", check.GetProperty("status").GetString());
        Assert.Equal("CDC.Api is unreachable", check.GetProperty("description").GetString());
        Assert.Equal("boom", check.GetProperty("exception").GetString());
    }
}
