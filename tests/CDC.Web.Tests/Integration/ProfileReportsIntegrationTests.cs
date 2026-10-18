using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the real Profile reports page; only the CDC.Api client and the report generator are faked.
public class ProfileReportsIntegrationTests
{
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid StoredReportId = Guid.NewGuid();
    private static readonly byte[] StoredPdf = "%PDF-1.7 stored"u8.ToArray();

    [Fact]
    public async Task ProfileReports_RendersTheBreadcrumbTitleTableAndLinks()
    {
        using var factory = CreateFactory(CreateApi());
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ProfileReports/{ProfileVersionId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("D2R2 Home", body);
        Assert.Contains("Search disease profiles", body);
        Assert.Contains("govuk-breadcrumbs__list-item\" aria-current=\"page\"", body);
        Assert.Contains("Profile reports for Anthrax v1.2", body);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Title</th>", body);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Action</th>", body);
        Assert.Contains("handler=Download", body);
        Assert.Contains($"reportId={StoredReportId}", body);
        Assert.Contains("Download PDF", body);
        Assert.Contains("Regenerate report", body);
        Assert.Contains($"/SurveillanceProfiles/CreateBespokeReport/{ProfileVersionId}", body);
        Assert.Contains("Create bespoke report", body);
        Assert.Contains($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}", body);
        Assert.Contains("Browse disease profile", body);
        Assert.DoesNotContain("profile-reports-intro", body);
    }

    [Fact]
    public async Task ProfileReports_DownloadsTheStoredPdf()
    {
        using var factory = CreateFactory(CreateApi());
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ProfileReports/{ProfileVersionId}?handler=Download&reportId={StoredReportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(StoredPdf, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Anthrax_Summary profile.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task ProfileReports_ShowsTheErrorSummary_WhenTheReportsCannotBeLoaded()
    {
        using var factory = CreateFactory(new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Error)));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ProfileReports/{ProfileVersionId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("We could not load the profile reports", body);
    }

    [Fact]
    public async Task ProfileReports_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        using var factory = CreateFactory(new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.NotFound)));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ProfileReports/{ProfileVersionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static FakeProfileReportsApiService CreateApi() => new(
        overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Success, new ProfileReportsOverviewDto
        {
            ProfileVersionId = ProfileVersionId,
            ProfileId = ProfileId,
            FullTitle = "Anthrax",
            VersionLabel = "1.2",
            IsCurrent = true,
            CanCreateReports = true,
            Reports =
            [
                new ProfileReportListItemDto { Id = StoredReportId, ReportName = "SummaryProfileGUID", DisplayName = "Summary profile", HasPdfData = true, FileSize = 12 },
                new ProfileReportListItemDto { Id = Guid.NewGuid(), ReportName = "QABriefGUID", DisplayName = "QA brief" }
            ]
        }),
        storedReport: StoredPdf);

    private static WebApplicationFactory<Program> CreateFactory(IProfileReportsApiService reports) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton(reports);
                services.RemoveAll<IProfileReportGenerator>();
                services.AddSingleton<IProfileReportGenerator>(new SummaryOnlyGenerator());
            }));

    private sealed class SummaryOnlyGenerator : IProfileReportGenerator
    {
        public bool CanGenerate(string reportName) => reportName == "SummaryProfileGUID";

        public Task<byte[]> GenerateAsync(string reportName, Guid profileVersionId, CancellationToken cancellationToken) =>
            Task.FromResult(StoredPdf);
    }
}
