using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the real Razor view through the real view renderer; only the CDC.Api client and the
// Chromium PDF step are faked (the latter captures the HTML it was asked to print).
public class ContributionsReportIntegrationTests
{
    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();

    [Fact]
    public async Task ContributionsReport_RendersTheVersionHeadingsSectionTablesAndEmptyRows_AndReturnsThePdf()
    {
        var profileId = Guid.NewGuid();
        var report = new ContributionsReportDto
        {
            ProfileId = profileId,
            ProfileTitle = "Anthrax",
            Versions =
            [
                new ContributionsReportVersionDto
                {
                    FullTitle = "Anthrax",
                    VersionMajor = 2,
                    VersionMinor = 0,
                    EffectiveDateFrom = new DateTime(2026, 3, 1),
                    IsCurrent = true,
                    ProfileStatusName = "Active",
                    Sections =
                    [
                        new ContributionsReportSectionDto
                        {
                            SectionNumber = 1,
                            SectionName = "Summary",
                            Contributions =
                            [
                                new ProfileSectionContributionDto { FullName = "Ann <Example>", OrganisationName = "APHA", LastContributionDate = new DateTime(2026, 2, 14) }
                            ]
                        },
                        new ContributionsReportSectionDto { SectionNumber = 2, SectionName = "Pathogen" }
                    ]
                },
                new ContributionsReportVersionDto
                {
                    FullTitle = "Anthrax",
                    VersionMajor = 1,
                    VersionMinor = 0,
                    EffectiveDateFrom = new DateTime(2025, 1, 1),
                    EffectiveDateTo = new DateTime(2026, 3, 1)
                }
            ]
        };
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(new FixedReportsApi(new ContributionsReportResult(ContributionsReportOutcome.Success, report)), pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewContributionsReport/{profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Anthrax_Contributions.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());

        var html = pdf.Html!;
        Assert.Contains("Anthrax - v2.0 (Draft) Valid from 01/03/2026 (Active)", html);
        Assert.Contains("Anthrax - v1.0 (Draft) Valid from 01/01/2025 to 01/03/2026", html);
        Assert.Contains("1. Summary", html);
        Assert.Contains("Ann &lt;Example&gt;", html);
        Assert.Contains("14/02/2026", html);
        Assert.Contains("- No contributions -", html);
        Assert.Contains("<th scope=\"col\">Organisation</th>", html);
    }

    [Fact]
    public async Task ContributionsReport_ShowsTheErrorPage_WhenThePdfCannotBeRendered()
    {
        var report = new ContributionsReportDto { ProfileTitle = "Anthrax", Versions = [new ContributionsReportVersionDto { FullTitle = "Anthrax" }] };
        using var factory = CreateFactory(
            new FixedReportsApi(new ContributionsReportResult(ContributionsReportOutcome.Success, report)),
            new CapturingPdfService(new PdfRenderException("no browser")));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewContributionsReport/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("We could not create the contributions report", body);
        Assert.Contains("Back to manage profile", body);
    }

    [Fact]
    public async Task ContributionsReport_ReturnsNotFound_WhenTheProfileHasNoDraftVersions()
    {
        using var factory = CreateFactory(new FixedReportsApi(new ContributionsReportResult(ContributionsReportOutcome.NotFound)), new CapturingPdfService());
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewContributionsReport/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(IProfileReportsApiService reports, IPlaywrightPdfService pdf) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton(reports);
                services.RemoveAll<IPlaywrightPdfService>();
                services.AddSingleton(pdf);
            }));

    private sealed class FixedReportsApi(ContributionsReportResult result) : IProfileReportsApiService
    {
        public Task<ContributionsReportResult> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class CapturingPdfService(Exception? throwOnRender = null) : IPlaywrightPdfService
    {
        public string? Html { get; private set; }

        public Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
        {
            Html = html;

            return throwOnRender is not null ? throw throwOnRender : Task.FromResult(PdfBytes);
        }
    }
}
