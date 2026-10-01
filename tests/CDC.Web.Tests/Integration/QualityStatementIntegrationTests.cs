using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api to call, so this swaps in a
// fake IStaticReportsApiService to exercise the quality statement download's success path.
public class QualityStatementIntegrationTests
{
    private static readonly Guid VersionId = Guid.Parse("c80b8e93-21d2-453a-b0e0-3f522d03971d");
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D]; // "%PDF-"

    [Fact]
    public async Task QualityStatement_DownloadsCurrentVersion_WhenApiReturnsData()
    {
        var currentReports = new List<StaticReportVersionDto>
        {
            new()
            {
                Id = VersionId,
                Title = "D2R2 Quality Statement",
                IsUserManual = true,
                IsCurrent = true
            }
        };
        var fakeService = new FakeStaticReportsApiService(
            currentReports,
            new StaticReportDataDto { PdfData = PdfBytes, Title = "D2R2 Quality Statement" });

        using var factory = CreateFactory(fakeService);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/QualityStatement");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        // Matches legacy's PdfDownloadHelper.DownloadPdfBytes: "{Title}_{dd_MM_yyyy_HH_mm_ss}.pdf".
        Assert.Matches(
            @"^D2R2_Quality_Statement_\d{2}_\d{2}_\d{4}_\d{2}_\d{2}_\d{2}\.pdf$",
            response.Content.Headers.ContentDisposition!.FileName ?? string.Empty);
        Assert.Equal(PdfBytes, bytes);
    }

    [Fact]
    public async Task QualityStatement_ReturnsNotFound_WhenNoCurrentVersionExists()
    {
        using var factory = CreateFactory(new FakeStaticReportsApiService());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/QualityStatement");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QualityStatement_ReturnsNotFound_WhenCurrentVersionHasNoData()
    {
        var currentReports = new List<StaticReportVersionDto>
        {
            new()
            {
                Id = VersionId,
                Title = "D2R2 Quality Statement",
                IsUserManual = true,
                IsCurrent = true
            }
        };
        using var factory = CreateFactory(new FakeStaticReportsApiService(currentReports, data: null));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/QualityStatement");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(IStaticReportsApiService fakeService) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaticReportsApiService>();
                services.AddSingleton(fakeService);
            }));
}
