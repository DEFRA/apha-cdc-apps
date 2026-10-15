using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
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
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ViewProfilePrintVersionModelTests
{
    private static readonly DateTime PrintedAt = new(2026, 10, 14, 22, 13, 32, DateTimeKind.Utc);
    private static readonly byte[] PdfBytes = "%PDF-1.7 print"u8.ToArray();

    [Fact]
    public async Task OnGetAsync_ReturnsTheFullProfileAsAnAttachment_NamedAsTheLegacyPageNamedIt()
    {
        var reports = ReportsWith(TwoSectionContent());
        var renderer = new FakeViewRenderer("<html>print</html>");
        var pdf = new FakePdfService(PdfBytes);

        var result = await CreatePage(reports: reports, renderer: renderer, pdf: pdf).OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(PdfBytes, file.FileContents);
        Assert.Equal("Bovine tuberculosis(Draft)_18.7_Print full profile_14_10_2026_22_13_32.pdf", file.FileDownloadName);
        Assert.Equal(ProfileVersionId, reports.RequestedContentVersionId);
        Assert.Equal(ProfileReportDocumentPdfBuilder.ViewPath, renderer.ViewPath);
        var document = Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model);
        Assert.Equal("Full profile", document.ReportTitle);
        Assert.True(document.ShowFrontPage);
        Assert.Equal(["1. One", "2. Two"], document.Sections.Select(section => section.Heading));
        Assert.Equal("<html>print</html>", pdf.Html);
    }

    [Fact]
    public async Task OnGetAsync_PrintsOnlyTheRequestedSection_WithoutAFrontPage()
    {
        var content = TwoSectionContent();
        var renderer = new FakeViewRenderer("<html/>");

        var result = await CreatePage(reports: ReportsWith(content), renderer: renderer, section: content.Sections[1].Id).OnGetAsync(CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        var document = Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model);
        Assert.False(document.ShowFrontPage);
        Assert.Equal("2. Two", Assert.Single(document.Sections).Heading);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileHasNoSuchSection()
    {
        var renderer = new FakeViewRenderer("<html/>");

        var result = await CreatePage(renderer: renderer, section: Guid.NewGuid()).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(renderer.Model);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsBadRequest_WhenTheSectionIsNotAGuid_InsteadOfPrintingTheWholeProfile()
    {
        var reports = ReportsWith(TwoSectionContent());
        var page = CreatePage(reports: reports);
        page.ModelState.AddModelError(nameof(ViewProfilePrintVersionModel.Section), "The value 'abc' is not valid.");

        Assert.IsType<BadRequestResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Null(reports.RequestedContentVersionId);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var reports = ReportsWith(TwoSectionContent());

        var result = await CreatePage(apiClient: new FakeApiClient(manageProfile: null), reports: reports).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(reports.RequestedContentVersionId);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileHasNoCurrentVersion()
    {
        var reports = ReportsWith(TwoSectionContent());
        var apiClient = ApiClientFor(PrintableDraft() with { CurrentProfileVersionId = Guid.Empty });

        var result = await CreatePage(apiClient: apiClient, reports: reports).OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(reports.RequestedContentVersionId);
    }

    [Fact]
    public async Task OnGetAsync_Returns403_WithoutReadingTheContent_WhenTheUserMayNotPrintTheCurrentVersion()
    {
        var reports = ReportsWith(TwoSectionContent());
        var logger = new AlwaysEnabledLogger<ViewProfilePrintVersionModel>();
        var apiClient = ApiClientFor(PrintableDraft() with { CanPrintProfileSection = false });

        var result = await CreatePage(apiClient: apiClient, reports: reports, logger: logger).OnGetAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Null(reports.RequestedContentVersionId);
        Assert.Contains(logger.Messages, message => message.Contains(ProfileId.ToString(), StringComparison.Ordinal) && message.Contains("refused", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheProfileCannotBeLoaded(Type exceptionType)
    {
        var reports = ReportsWith(TwoSectionContent());
        var logger = new AlwaysEnabledLogger<ViewProfilePrintVersionModel>();
        var apiClient = new FakeApiClient(throwOnGetManageProfile: (Exception)Activator.CreateInstance(exceptionType)!);
        var page = CreatePage(apiClient: apiClient, reports: reports, logger: logger);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewProfilePrintVersionModel.LoadFailureMessage, page.ErrorMessage);
        Assert.Null(reports.RequestedContentVersionId);
        Assert.Contains(logger.Messages, message => message.Contains(ProfileId.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheContentCannotBeRead(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<ViewProfilePrintVersionModel>();
        var reports = new FakeProfileReportsApiService(throwOnContent: (Exception)Activator.CreateInstance(exceptionType)!);
        var page = CreatePage(reports: reports, logger: logger);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewProfilePrintVersionModel.BuildFailureMessage, page.ErrorMessage);
        Assert.Contains(logger.Messages, message => message.Contains(ProfileVersionId.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ProfileReportsOutcome.NotFound)]
    [InlineData(ProfileReportsOutcome.Forbidden)]
    [InlineData(ProfileReportsOutcome.Error)]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheApiRefusesTheContent(ProfileReportsOutcome outcome)
    {
        var page = CreatePage(reports: new FakeProfileReportsApiService(content: new ProfileReportContentResult(outcome)));

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewProfilePrintVersionModel.BuildFailureMessage, page.ErrorMessage);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheReferencesCannotBeLoaded()
    {
        var sections = new FakeProfileSectionsApiService(throwOnGetProfileNoteTypes: new HttpRequestException("api down"));
        var page = CreatePage(sections: sections);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewProfilePrintVersionModel.BuildFailureMessage, page.ErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenRenderingFails(bool failInViewRenderer)
    {
        var renderer = new FakeViewRenderer("<html/>", failInViewRenderer ? new InvalidOperationException("view missing") : null);
        var pdf = new FakePdfService(PdfBytes, failInViewRenderer ? null : new PdfRenderException("no browser"));
        var page = CreatePage(renderer: renderer, pdf: pdf);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewProfilePrintVersionModel.BuildFailureMessage, page.ErrorMessage);
    }

    [Fact]
    public async Task OnGetAsync_NamesAPublishedVersionAsPublished()
    {
        var apiClient = ApiClientFor(PrintableDraft() with { CurrentVersionIsPublished = true });

        var file = Assert.IsType<FileContentResult>(await CreatePage(apiClient: apiClient).OnGetAsync(CancellationToken.None));

        Assert.Equal("Bovine tuberculosis(Published)_18.7_Print full profile_14_10_2026_22_13_32.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task OnGetAsync_NamesTheFileAfterTheFullTitleAsPlainText_ReplacingCharactersThatAreInvalidInFileNames()
    {
        var apiClient = ApiClientFor(PrintableDraft() with { ProfileTitle = "Foot/and\\mouth <em>disease</em>", IsWhatIfScenario = true, ScenarioTitle = "Worst case" });

        var file = Assert.IsType<FileContentResult>(await CreatePage(apiClient: apiClient).OnGetAsync(CancellationToken.None));

        Assert.Equal("Foot_and_mouth disease (Worst case)(Draft)_18.7_Print full profile_14_10_2026_22_13_32.pdf", file.FileDownloadName);
    }

    [Fact]
    public void PageTitle_NamesTheWholeProfileOrOneSection()
    {
        Assert.Equal("Print full profile", CreatePage().PageTitle);
        Assert.Equal("Print profile section", CreatePage(section: Guid.NewGuid()).PageTitle);
    }

    private static ProfileReportContentDto TwoSectionContent() =>
        ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);

    private static FakeProfileReportsApiService ReportsWith(ProfileReportContentDto content) =>
        new(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, content));

    private static ManageProfileViewModel PrintableDraft() => DraftProfile() with { CurrentVersionNumber = "18.7" };

    private static FakeApiClient ApiClientFor(ManageProfileViewModel profile) => new(manageProfile: profile);

    private static ViewProfilePrintVersionModel CreatePage(
        FakeApiClient? apiClient = null,
        FakeProfileReportsApiService? reports = null,
        FakeViewRenderer? renderer = null,
        FakePdfService? pdf = null,
        FakeProfileSectionsApiService? sections = null,
        AlwaysEnabledLogger<ViewProfilePrintVersionModel>? logger = null,
        Guid? section = null)
    {
        var builder = new FullProfileReportPdfBuilder(
            reports ?? ReportsWith(TwoSectionContent()),
            renderer ?? new FakeViewRenderer("<html/>"),
            pdf ?? new FakePdfService(PdfBytes),
            new ProfileReportReferenceLoader(sections ?? new FakeProfileSectionsApiService()));

        return new ViewProfilePrintVersionModel(
            apiClient ?? ApiClientFor(PrintableDraft()),
            builder,
            logger ?? new AlwaysEnabledLogger<ViewProfilePrintVersionModel>(),
            new FixedTimeProvider(PrintedAt))
        {
            ProfileId = ProfileId,
            Section = section,
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
