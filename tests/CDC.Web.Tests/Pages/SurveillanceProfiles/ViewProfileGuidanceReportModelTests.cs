using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ViewProfileGuidanceReportModelTests
{
    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();
    private static readonly DateTime ProducedAt = new(2027, 3, 4, 14, 5, 6);

    [Theory]
    [InlineData(null, GuidanceReportType.All, "Profile Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData("", GuidanceReportType.All, "Profile Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData("All", GuidanceReportType.All, "Profile Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData("summaryprofile", GuidanceReportType.SummaryProfile, "Summary Profile Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData("SummaryPrioritisationReport", GuidanceReportType.SummaryPrioritisationReport, "Summary Prioritisation Report Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData("QAGuidanceReport", GuidanceReportType.QAGuidanceReport, "Q&A Guidance Report_04_03_2027_14_05_06.pdf")]
    public async Task OnGetAsync_ReturnsThePdfAsAnAttachment_NamedWithTheTimeItWasProduced(string? type, GuidanceReportType expectedType, string expectedFileName)
    {
        var pdf = new FakePdfService(PdfBytes);
        var model = CreatePage(pdf: pdf);
        model.Type = type;

        var result = await model.OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(PdfBytes, file.FileContents);
        Assert.Equal(expectedFileName, file.FileDownloadName);
        Assert.Contains(GuidanceReportLayoutTitle(expectedType), pdf.Options!.FooterHtml!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("All", ViewProfileGuidanceReportModel.GuidanceViewPath, typeof(GuidanceReportViewModel))]
    [InlineData("SummaryProfile", ViewProfileGuidanceReportModel.GuidanceViewPath, typeof(GuidanceReportViewModel))]
    [InlineData("QAGuidanceReport", ViewProfileGuidanceReportModel.GuidanceViewPath, typeof(GuidanceReportViewModel))]
    [InlineData("SummaryPrioritisationReport", ViewProfileGuidanceReportModel.SprViewPath, typeof(SprGuidanceReportViewModel))]
    public async Task OnGetAsync_RendersTheViewForTheReport(string type, string expectedView, Type expectedModel)
    {
        var renderer = new FakeViewRenderer("<html/>");
        var model = CreatePage(renderer: renderer);
        model.Type = type;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedView, renderer.ViewPath);
        Assert.IsType(expectedModel, renderer.Model);
    }

    [Theory]
    [InlineData("Nonsense")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("0")]
    public async Task OnGetAsync_RefusesAnUnknownReportType_WithoutCallingTheApi(string type)
    {
        var logger = new AlwaysEnabledLogger<ViewProfileGuidanceReportModel>();
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(questions: questions, logger: logger);
        model.Type = type;

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(0, questions.GuidanceContentCallCount);
        Assert.Contains(logger.Messages, message => message.Contains(type, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsForbidden_WhenTheApiRefuses()
    {
        var logger = new AlwaysEnabledLogger<ViewProfileGuidanceReportModel>();
        var questions = new FakeProfileQuestionsApiService(guidanceContent: new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Forbidden));
        var model = CreatePage(questions: questions, logger: logger);

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Contains(logger.Messages, message => message.Contains("refused", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsAnError_WhenTheGuidanceCannotBeLoaded(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<ViewProfileGuidanceReportModel>();
        var questions = new FakeProfileQuestionsApiService(throwOnGetGuidanceContent: (Exception)Activator.CreateInstance(exceptionType)!);
        var model = CreatePage(questions: questions, logger: logger);

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ViewProfileGuidanceReportModel.FailureMessage, model.ErrorMessage);
        Assert.Contains(logger.Messages, message => message.Contains("Failed to load", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenAQuestionTheReportPrintsIsMissing()
    {
        var content = new ProfileGuidanceContentDto { Sections = [new ProfileGuidanceSectionDto { SectionNumber = 1, Questions = [] }] };
        var questions = new FakeProfileQuestionsApiService(guidanceContent: new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Success, content));
        var model = CreatePage(questions: questions);
        model.Type = "SummaryProfile";

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ViewProfileGuidanceReportModel.FailureMessage, model.ErrorMessage);
    }

    [Theory]
    [InlineData(typeof(PdfRenderException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task OnGetAsync_ShowsAnError_WhenThePdfCannotBeMade(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<ViewProfileGuidanceReportModel>();
        var exception = exceptionType == typeof(PdfRenderException) ? new PdfRenderException("no browser") : (Exception)new InvalidOperationException("bad view");
        var model = CreatePage(pdf: new FakePdfService(PdfBytes, exception), logger: logger);

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ViewProfileGuidanceReportModel.FailureMessage, model.ErrorMessage);
        Assert.Contains(logger.Messages, message => message.Contains("Failed to render", StringComparison.Ordinal));
    }

    private static string GuidanceReportLayoutTitle(GuidanceReportType type) => type switch
    {
        GuidanceReportType.All => "Full profile guidance report",
        GuidanceReportType.SummaryProfile => "Summary profile guidance report",
        GuidanceReportType.SummaryPrioritisationReport => "Guidance for questions in the summary prioritisation report (SPR)",
        _ => "Q&amp;A sheet guidance report"
    };

    private static ViewProfileGuidanceReportModel CreatePage(
        FakeProfileQuestionsApiService? questions = null,
        FakeViewRenderer? renderer = null,
        FakePdfService? pdf = null,
        AlwaysEnabledLogger<ViewProfileGuidanceReportModel>? logger = null) =>
        new(
            questions ?? new FakeProfileQuestionsApiService(guidanceContent: new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Success, GuidanceTestContent.Build())),
            renderer ?? new FakeViewRenderer("<html/>"),
            pdf ?? new FakePdfService(PdfBytes),
            logger ?? new AlwaysEnabledLogger<ViewProfileGuidanceReportModel>(),
            new FixedTimeProvider(ProducedAt))
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
