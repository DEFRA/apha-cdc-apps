using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using CDC.Web.Tests.Infrastructure;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ViewDraftSummaryReportModelTests
{
    private const string SummaryProfileName = "SummaryProfile";
    private const string SummaryPrioritisationName = "SummaryPrioritisation";

    private static readonly DateTime ProducedAt = new(2026, 10, 14, 22, 13, 32, DateTimeKind.Utc);
    private static readonly byte[] PdfBytes = "%PDF-1.7 summary"u8.ToArray();

    [Fact]
    public async Task OnGetAsync_SummaryProfile_ReturnsThePdfAsAnAttachment_NamedAsTheLegacyPageNamedIt_WithoutPrioritising()
    {
        var reports = ReportsWith(CreateContent());
        var renderer = new FakeViewRenderer("<html>summary</html>");
        var pdf = new FakePdfService(PdfBytes);

        var result = await CreatePage(SummaryProfileName, reports: reports, renderer: renderer, pdf: pdf).OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(PdfBytes, file.FileContents);
        Assert.Equal("Bovine tuberculosis(Draft)_18.7_Summary profile report_14_10_2026_22_13_32.pdf", file.FileDownloadName);
        Assert.Equal(["Content"], reports.Calls);
        Assert.Equal(ProfileVersionId, reports.RequestedContentVersionId);
        Assert.Equal(ProfileReportDocumentPdfBuilder.ViewPath, renderer.ViewPath);
        Assert.Equal("Summary profile", Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model).ReportTitle);
        Assert.Equal("<html>summary</html>", pdf.Html);
    }

    [Fact]
    public async Task OnGetAsync_SummaryPrioritisation_RecalculatesTheScoresFirst_ThenPrintsTheReport()
    {
        var reports = ReportsWith(CreateContent(), ProfilePrioritisationDocumentsTests.Scores());
        var renderer = new FakeViewRenderer("<html>prioritisation</html>");

        var result = await CreatePage(SummaryPrioritisationName, reports: reports, renderer: renderer).OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(PdfBytes, file.FileContents);
        Assert.Equal("Bovine tuberculosis(Draft)_18.7_Summary prioritisation report_14_10_2026_22_13_32.pdf", file.FileDownloadName);
        Assert.Equal(["Prioritise", "Content", "Scores"], reports.Calls);
        Assert.Equal(ProfileVersionId, reports.PrioritisedVersionId);
        Assert.Equal(SummaryPrioritisationReportPdfBuilder.ViewPath, renderer.ViewPath);
        Assert.Equal("Summary prioritisation report", Assert.IsType<PrioritisationDocumentViewModel>(renderer.Model).ReportTitle);
    }

    [Theory]
    [InlineData("summaryprofile")]
    [InlineData("SUMMARYPROFILE")]
    public async Task OnGetAsync_AcceptsTheReportNameInAnyCase(string name)
    {
        var result = await CreatePage(name).OnGetAsync(CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nope")]
    [InlineData("0")]
    [InlineData("SummaryProfile,SummaryPrioritisation")]
    public async Task OnGetAsync_ReturnsNotFound_WithoutCallingTheApi_ForAnUnknownReport(string? name)
    {
        var logger = new AlwaysEnabledLogger<ViewDraftSummaryReportModel>();
        var apiClient = new FakeApiClient(throwOnGetManageProfile: new InvalidOperationException("The API must not be called."));

        var result = await CreatePage(name, apiClient: apiClient, logger: logger).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Contains(logger.Messages, message => message.Contains("Rejected the unknown draft summary report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var reports = ReportsWith(CreateContent());

        var result = await CreatePage(SummaryProfileName, apiClient: new FakeApiClient(manageProfile: null), reports: reports).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(reports.Calls);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileHasNoCurrentVersion()
    {
        var reports = ReportsWith(CreateContent());
        var apiClient = ApiClientFor(RunnableDraft() with { CurrentProfileVersionId = Guid.Empty });

        var result = await CreatePage(SummaryProfileName, apiClient: apiClient, reports: reports).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(reports.Calls);
    }

    [Theory]
    [InlineData(SummaryProfileName, "You do not have permission to get the summary profile report of this profile version")]
    [InlineData(SummaryPrioritisationName, "You do not have permission to get the summary prioritisation report of this profile version")]
    public async Task OnGetAsync_SendsTheUserToTheApplicationErrorPage_WithoutReadingAnything_WhenTheyMayNotRunTheReport(string name, string details)
    {
        var reports = ReportsWith(CreateContent(), ProfilePrioritisationDocumentsTests.Scores());
        var logger = new AlwaysEnabledLogger<ViewDraftSummaryReportModel>();
        var apiClient = ApiClientFor(RunnableDraft() with { CanViewProfileReports = false });
        var page = CreatePage(name, apiClient: apiClient, reports: reports, logger: logger);
        var tempData = WithTempData(page);

        var result = await page.OnGetAsync(CancellationToken.None);

        AssertSentToTheApplicationErrorPage(result, tempData, details);
        Assert.Empty(reports.Calls);
        Assert.Contains(logger.Messages, message => message.Contains(ProfileId.ToString(), StringComparison.Ordinal) && message.Contains("refused", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_SendsTheUserToTheApplicationErrorPage_WhenCdcApiRefusesTheProfile()
    {
        var reports = ReportsWith(CreateContent());
        var apiClient = new FakeApiClient(throwOnGetManageProfile: Refused());
        var page = CreatePage(SummaryProfileName, apiClient: apiClient, reports: reports);
        var tempData = WithTempData(page);

        var result = await page.OnGetAsync(CancellationToken.None);

        AssertSentToTheApplicationErrorPage(result, tempData, "You do not have permission to get the summary profile report of this profile version");
        Assert.Empty(reports.Calls);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheProfileCannotBeLoaded(Type exceptionType)
    {
        var reports = ReportsWith(CreateContent());
        var logger = new AlwaysEnabledLogger<ViewDraftSummaryReportModel>();
        var apiClient = new FakeApiClient(throwOnGetManageProfile: (Exception)Activator.CreateInstance(exceptionType)!);
        var page = CreatePage(SummaryProfileName, apiClient: apiClient, reports: reports, logger: logger);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewDraftSummaryReportModel.LoadFailureMessage, page.ErrorMessage);
        Assert.Empty(reports.Calls);
        Assert.Contains(logger.Messages, message => message.Contains(ProfileId.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SummaryProfileName, typeof(HttpRequestException), "We could not create the summary profile report. Try again later.")]
    [InlineData(SummaryProfileName, typeof(TaskCanceledException), "We could not create the summary profile report. Try again later.")]
    [InlineData(SummaryProfileName, typeof(NotSupportedException), "We could not create the summary profile report. Try again later.")]
    [InlineData(SummaryPrioritisationName, typeof(HttpRequestException), "We could not create the summary prioritisation report. Try again later.")]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheContentCannotBeRead(string name, Type exceptionType, string message)
    {
        var logger = new AlwaysEnabledLogger<ViewDraftSummaryReportModel>();
        var reports = new FakeProfileReportsApiService(throwOnContent: (Exception)Activator.CreateInstance(exceptionType)!);
        var page = CreatePage(name, reports: reports, logger: logger);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(message, page.ErrorMessage);
        Assert.Contains(logger.Messages, entry => entry.Contains(ProfileVersionId.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ProfileReportsOutcome.NotFound)]
    [InlineData(ProfileReportsOutcome.Forbidden)]
    [InlineData(ProfileReportsOutcome.Error)]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheApiRefusesTheContent(ProfileReportsOutcome outcome)
    {
        var page = CreatePage(SummaryProfileName, reports: new FakeProfileReportsApiService(content: new ProfileReportContentResult(outcome)));

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal("We could not create the summary profile report. Try again later.", page.ErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenRenderingFails(bool failInViewRenderer)
    {
        var renderer = new FakeViewRenderer("<html/>", failInViewRenderer ? new InvalidOperationException("view missing") : null);
        var pdf = new FakePdfService(PdfBytes, failInViewRenderer ? null : new PdfRenderException("no browser"));
        var page = CreatePage(SummaryProfileName, renderer: renderer, pdf: pdf);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal("We could not create the summary profile report. Try again later.", page.ErrorMessage);
    }

    [Fact]
    public async Task OnGetAsync_SummaryPrioritisation_ReturnsNotFound_WithoutReadingTheReport_WhenTheVersionIsGone()
    {
        var reports = ReportsWith(CreateContent(), ProfilePrioritisationDocumentsTests.Scores(), ProfileReportsOutcome.NotFound);

        var result = await CreatePage(SummaryPrioritisationName, reports: reports).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(["Prioritise"], reports.Calls);
    }

    [Fact]
    public async Task OnGetAsync_SummaryPrioritisation_SendsTheUserToTheApplicationErrorPage_WhenCdcApiRefusesToPrioritise()
    {
        var reports = ReportsWith(CreateContent(), ProfilePrioritisationDocumentsTests.Scores(), ProfileReportsOutcome.Forbidden);
        var logger = new AlwaysEnabledLogger<ViewDraftSummaryReportModel>();
        var page = CreatePage(SummaryPrioritisationName, reports: reports, logger: logger);
        var tempData = WithTempData(page);

        var result = await page.OnGetAsync(CancellationToken.None);

        AssertSentToTheApplicationErrorPage(result, tempData, ViewDraftSummaryReportModel.PrioritiseRefusedDetails);
        Assert.Equal(["Prioritise"], reports.Calls);
        Assert.Contains(logger.Messages, message => message.Contains(ProfileVersionId.ToString(), StringComparison.Ordinal) && message.Contains("Forbidden", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_SummaryPrioritisation_ShowsTheErrorPage_WithoutReadingTheReport_WhenPrioritisingFails()
    {
        var reports = ReportsWith(CreateContent(), ProfilePrioritisationDocumentsTests.Scores(), ProfileReportsOutcome.Error);
        var page = CreatePage(SummaryPrioritisationName, reports: reports);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal("We could not create the summary prioritisation report. Try again later.", page.ErrorMessage);
        Assert.Equal(["Prioritise"], reports.Calls);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task OnGetAsync_SummaryPrioritisation_ShowsTheErrorPage_WhenThePrioritiseCallFails(Type exceptionType)
    {
        var reports = new FakeProfileReportsApiService(throwOnPrioritise: (Exception)Activator.CreateInstance(exceptionType)!);
        var page = CreatePage(SummaryPrioritisationName, reports: reports);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal("We could not create the summary prioritisation report. Try again later.", page.ErrorMessage);
        Assert.Equal(["Prioritise"], reports.Calls);
    }

    [Fact]
    public async Task OnGetAsync_NamesAPublishedVersionAsPublished()
    {
        var apiClient = ApiClientFor(RunnableDraft() with { CurrentVersionIsPublished = true });

        var file = Assert.IsType<FileContentResult>(await CreatePage(SummaryProfileName, apiClient: apiClient).OnGetAsync(CancellationToken.None));

        Assert.Equal("Bovine tuberculosis(Published)_18.7_Summary profile report_14_10_2026_22_13_32.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task OnGetAsync_NamesTheFileAfterTheFullTitleAsPlainText_ReplacingCharactersThatAreInvalidInFileNames()
    {
        var apiClient = ApiClientFor(RunnableDraft() with { ProfileTitle = "Foot/and\\mouth <em>disease</em>", IsWhatIfScenario = true, ScenarioTitle = "Worst case" });

        var file = Assert.IsType<FileContentResult>(await CreatePage(SummaryProfileName, apiClient: apiClient).OnGetAsync(CancellationToken.None));

        Assert.Equal("Foot_and_mouth disease (Worst case)(Draft)_18.7_Summary profile report_14_10_2026_22_13_32.pdf", file.FileDownloadName);
    }

    [Theory]
    [InlineData(SummaryProfileName, DraftSummaryReport.SummaryProfile, "Summary profile report")]
    [InlineData(SummaryPrioritisationName, DraftSummaryReport.SummaryPrioritisation, "Summary prioritisation report")]
    public void ReportType_AndPageTitle_NameTheReportTheRouteAsksFor(string name, DraftSummaryReport expected, string title)
    {
        var page = CreatePage(name);

        Assert.Equal(expected, page.ReportType);
        Assert.Equal(title, page.PageTitle);
        Assert.Equal(title, ViewDraftSummaryReportModel.TitleOf(expected));
    }

    [Fact]
    public void ReportType_IsNull_AndPageTitleIsGeneric_WhenTheRouteNamesNoReport()
    {
        var page = CreatePage("Nope");

        Assert.Null(page.ReportType);
        Assert.Equal("Summary report", page.PageTitle);
    }

    private static void AssertSentToTheApplicationErrorPage(IActionResult result, TempDataDictionary tempData, string details)
    {
        var redirect = Assert.IsType<RedirectToPageResult>(result);

        Assert.Equal(ApplicationErrorModel.PagePath, redirect.PageName);
        Assert.Contains(ProfileSectionTabPageModel.AccessRefusedErrorType, tempData.Values);
        Assert.Contains(details, tempData.Values);
        Assert.Contains($"https://d2r2.example/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/{SummaryProfileName}", tempData.Values);
    }

    private static ProfileReportContentDto CreateContent() =>
        ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);

    private static FakeProfileReportsApiService ReportsWith(
        ProfileReportContentDto content,
        ProfilePrioritisationScoresDto? scores = null,
        ProfileReportsOutcome prioritiseOutcome = ProfileReportsOutcome.Success) =>
        new(
            content: new ProfileReportContentResult(ProfileReportsOutcome.Success, content),
            prioritisationScores: new ProfilePrioritisationScoresResult(ProfileReportsOutcome.Success, scores ?? new ProfilePrioritisationScoresDto()),
            prioritiseOutcome: prioritiseOutcome);

    /// <summary>A current draft the user may run the summary reports on.</summary>
    private static ManageProfileViewModel RunnableDraft() =>
        DraftProfile() with { CurrentVersionNumber = "18.7", CanViewProfileReports = true };

    private static FakeApiClient ApiClientFor(ManageProfileViewModel profile) => new(manageProfile: profile);

    private static ViewDraftSummaryReportModel CreatePage(
        string? report,
        FakeApiClient? apiClient = null,
        FakeProfileReportsApiService? reports = null,
        FakeViewRenderer? renderer = null,
        FakePdfService? pdf = null,
        AlwaysEnabledLogger<ViewDraftSummaryReportModel>? logger = null)
    {
        var reportsApi = reports ?? ReportsWith(CreateContent());
        var viewRenderer = renderer ?? new FakeViewRenderer("<html/>");
        var pdfService = pdf ?? new FakePdfService(PdfBytes);
        var generator = new ProfileReportGenerator(
        [
            new SummaryProfileReportPdfBuilder(reportsApi, viewRenderer, pdfService),
            new SummaryPrioritisationReportPdfBuilder(reportsApi, viewRenderer, pdfService)
        ]);

        return new ViewDraftSummaryReportModel(
            apiClient ?? ApiClientFor(RunnableDraft()),
            reportsApi,
            generator,
            logger ?? new AlwaysEnabledLogger<ViewDraftSummaryReportModel>(),
            new FixedTimeProvider(ProducedAt))
        {
            ProfileId = ProfileId,
            Report = report,
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static TempDataDictionary WithTempData(ViewDraftSummaryReportModel page)
    {
        var tempData = ProfileTabTestData.WithTempData(page);
        page.Request.Path = $"/SurveillanceProfiles/ViewDraftSummaryReport/{ProfileId}/{SummaryProfileName}";

        return tempData;
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
