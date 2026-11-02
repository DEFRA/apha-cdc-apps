using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Infrastructure;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the real report page and the real report Razor views; only the CDC.Api clients and the Chromium PDF step are faked
// (the latter captures the HTML it was asked to print).
public class DraftSummaryReportIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly byte[] PdfBytes = "%PDF-1.7 summary"u8.ToArray();

    [Fact]
    public async Task SummaryProfile_DownloadsThePdfOfTheCurrentDraft_WithTheLegacyFileNameAndTitle()
    {
        var reports = CreateReports();
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile(), reports, pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/SummaryProfile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Matches(@"Anthrax\(Draft\)_18\.7_Summary profile report_\d{2}_\d{2}_\d{4}_\d{2}_\d{2}_\d{2}\.pdf", FileNameOf(response));
        Assert.Equal(["Content"], reports.Calls);

        var html = pdf.Html!;
        Assert.Contains("<title>Summary profile for Anthrax - v18.7 (Draft) Valid from 05/08/2026 to 16/08/2026</title>", html);
        Assert.Contains("Summary profile for: Anthrax", html);
        Assert.Contains("DRAFT CONTENT", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">1. Description</h2>", html);
        Assert.Contains("<h3>1.1 What is it?</h3>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Please Note</h2>", html);
    }

    [Fact]
    public async Task SummaryPrioritisation_RecalculatesTheScoresFirst_ThenDownloadsThePdfOfTheCurrentDraft()
    {
        var reports = CreateReports();
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile(), reports, pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/SummaryPrioritisation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Matches(@"Anthrax\(Draft\)_18\.7_Summary prioritisation report_\d{2}_\d{2}_\d{4}_\d{2}_\d{2}_\d{2}\.pdf", FileNameOf(response));
        Assert.Equal(["Prioritise", "Content", "Scores"], reports.Calls);
        Assert.Equal(ProfileVersionId, reports.PrioritisedVersionId);

        var html = pdf.Html!;
        Assert.Contains("Summary prioritisation report for: Anthrax", html);
        Assert.Contains("DRAFT CONTENT", html);
        Assert.Contains("<h3>Summary description</h3>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Detailed criteria scores: Cattle and Sheep</h2>", html);
    }

    [Fact]
    public async Task BrowseProfile_LinksBothReportsOfTheDraft_AndEachLinkDownloadsItsPdf()
    {
        var reports = CreateReports();
        using var factory = CreateFactory(CreateProfile(), reports, new FakePdfService(PdfBytes));
        var client = await factory.SignedInClientAsync();

        var browse = await (await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}")).Content.ReadAsStringAsync();
        var links = Regex.Matches(browse, "href=\"(/SurveillanceProfiles/ViewDraftSummaryReport/[^\"]+)\"").Select(match => match.Groups[1].Value).ToList();

        Assert.Equal(
            [
                $"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/SummaryProfile",
                $"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/SummaryPrioritisation"
            ],
            links);

        foreach (var link in links)
        {
            var response = await client.GetAsync(link);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Theory]
    [InlineData("SummaryProfile", "You do not have permission to get the summary profile report of this profile version")]
    [InlineData("SummaryPrioritisation", "You do not have permission to get the summary prioritisation report of this profile version")]
    public async Task Report_SendsAUserWhoMayNotRunIt_ToTheApplicationErrorPage_WithoutPrintingAnything(string report, string details)
    {
        var reports = CreateReports();
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile() with { CanViewProfileReports = false }, reports, pdf);
        var client = await factory.SignedInClientAsync();

        var shown = await client.GetAsync($"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/{report}");
        var body = await shown.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-xl\">Application error</h1>", body);
        Assert.Contains(ProfileSectionTabPageModel.AccessRefusedErrorType, body);
        Assert.Contains(details, body);
        Assert.Contains($"https://localhost/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/{report}", body);
        Assert.Empty(reports.Calls);
        Assert.Null(pdf.Html);
    }

    [Fact]
    public async Task Report_ReturnsNotFound_ForAnUnknownReportOrProfile()
    {
        using var factory = CreateFactory(profile: null, CreateReports(), new FakePdfService(PdfBytes));
        var client = await factory.SignedInClientAsync();

        var unknownReport = await client.GetAsync($"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/Nope");
        var unknownProfile = await client.GetAsync($"/SurveillanceProfiles/ViewDraftSummaryReport/{Guid.NewGuid()}/SummaryProfile");

        Assert.Equal(HttpStatusCode.NotFound, unknownReport.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownProfile.StatusCode);
    }

    [Theory]
    [InlineData("SummaryProfile", "Summary profile report", "summary profile report")]
    [InlineData("SummaryPrioritisation", "Summary prioritisation report", "summary prioritisation report")]
    public async Task Report_ShowsTheErrorPage_WhenThePdfCannotBeRendered(string report, string title, string inMessage)
    {
        using var factory = CreateFactory(CreateProfile(), CreateReports(), new FakePdfService(PdfBytes, new PdfRenderException("no browser")));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/{report}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"<title>Error: {title}", body);
        Assert.Contains("There is a problem", body);
        Assert.Contains($"We could not create the {inMessage}. Try again later.", body);
        Assert.Contains($"<h1 class=\"govuk-heading-l\">{title}</h1>", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}\"", body);
    }

    private static string? FileNameOf(HttpResponseMessage response) =>
        response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;

    private static ManageProfileViewModel CreateProfile() => ProfileTabTestData.DraftProfile() with
    {
        ProfileId = ProfileId,
        ProfileTitle = "Anthrax",
        CurrentProfileVersionId = ProfileVersionId,
        CurrentVersionNumber = "18.7",
        CurrentVersionValidFrom = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
        CurrentVersionValidTo = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc),
        CanViewProfileReports = true
    };

    private static ProfileReportContentDto CreateContent() => new()
    {
        ProfileVersionId = ProfileVersionId,
        ProfileId = ProfileId,
        FullTitle = "Anthrax",
        VersionLabel = "18.7",
        EffectiveDateFrom = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
        EffectiveDateTo = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc),
        ShowDraftWatermark = true,
        Sections = [ReportContent.Section(1, "Description", questions: ReportContent.Answered(1, "What is it?", "A disease"))]
    };

    private static FakeProfileReportsApiService CreateReports() => new(
        content: new ProfileReportContentResult(ProfileReportsOutcome.Success, CreateContent()),
        prioritisationScores: new ProfilePrioritisationScoresResult(ProfileReportsOutcome.Success, ProfilePrioritisationDocumentsTests.Scores()));

    private static WebApplicationFactory<Program> CreateFactory(ManageProfileViewModel? profile, FakeProfileReportsApiService reports, IPlaywrightPdfService pdf) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton<IProfileReportsApiService>(reports);
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata: ProfileTabTestData.TwoSectionMetadata()));
                services.RemoveAll<IPlaywrightPdfService>();
                services.AddSingleton(pdf);
            }));
}
