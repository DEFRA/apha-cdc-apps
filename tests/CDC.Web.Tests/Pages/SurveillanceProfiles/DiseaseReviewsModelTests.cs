using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class DiseaseReviewsModelTests
{
    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();
    private static readonly DateTime ProducedAt = new(2027, 3, 4, 14, 5, 6);
    private static readonly GlobalUserDto Jane = new() { Id = Guid.NewGuid(), FullName = "Jane Doe" };
    private static readonly GlobalUserDto Sam = new() { Id = Guid.NewGuid(), FullName = "Sam Poe" };

    [Fact]
    public async Task OnGetAsync_ListsEveryAuthor_WithTheFirstSelected()
    {
        var users = new StubGlobalUsersApiService([Jane, Sam]);
        var model = CreatePage(users: users);
        model.AuthorId = null;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal([Jane, Sam], model.Authors);
        Assert.Equal(Jane.Id, model.AuthorId);
        Assert.True(model.ModelState.IsValid);
        Assert.Equal((1, 0), users.RequestedPage);
    }

    [Fact]
    public async Task OnGetAsync_SelectsNoAuthor_WhenThereAreNone()
    {
        var model = CreatePage(users: new StubGlobalUsersApiService([]));

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.Authors);
        Assert.Null(model.AuthorId);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsAnError_WhenTheAuthorsCannotBeLoaded(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<DiseaseReviewsModel>();
        var model = CreatePage(users: new StubGlobalUsersApiService([], (Exception)Activator.CreateInstance(exceptionType)!), logger: logger);
        model.AuthorId = null;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.Authors);
        Assert.Null(model.AuthorId);
        Assert.Equal([DiseaseReviewsModel.AuthorsLoadFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Contains(logger.Messages, message => message.Contains("list of authors", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostAsync_ReturnsThePdfAsAnAttachment_NamedWithTheTimeItWasProduced()
    {
        var reports = ReportsReturning(new DiseaseReviewsReportDto
        {
            Sections = [new DiseaseReviewsReportRowDto { ProfileVersionId = Guid.NewGuid(), Title = "Anthrax", SectionNumber = 1, SectionName = "Summary" }]
        });
        var renderer = new FakeViewRenderer("<html>report</html>");
        var pdf = new FakePdfService(PdfBytes);

        var result = await CreatePage(reports, renderer: renderer, pdf: pdf).OnPostAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(PdfBytes, file.FileContents);
        Assert.Equal("DiseaseReviewsReport_04_03_2027_14_05_06.pdf", file.FileDownloadName);

        var requested = reports.RequestedDiseaseReviews!.Value;
        Assert.Equal(new DateTime(2027, 1, 1), requested.FromDate);
        Assert.Equal(new DateTime(2027, 12, 31), requested.ToDate);
        Assert.Equal(Jane.Id, requested.AuthorId);

        Assert.Equal(DiseaseReviewsModel.ReportViewPath, renderer.ViewPath);
        var view = Assert.IsType<DiseaseReviewsReportViewModel>(renderer.Model);
        Assert.Equal("<i>Quarterly</i> check", view.Description);
        Assert.Equal("Jane Doe due between 01-01-2027 and 31-12-2027", view.DateRange);
        Assert.Equal("Anthrax v0.0", Assert.Single(view.Profiles).Name);

        Assert.Equal("<html>report</html>", pdf.Html);
        Assert.Contains("Jane Doe due 01-01-27-31-12-27", pdf.Options!.FooterHtml);
        Assert.Contains("Produced: 04/03/2027.", pdf.Options.FooterHtml);
    }

    [Theory]
    [InlineData("1 January 2027", "31 December 2027")]
    [InlineData("1 Jan 2027", "31 Dec 2027")]
    [InlineData("01/01/2027", "31/12/2027")]
    [InlineData("1/1/2027", "31/12/2027")]
    [InlineData("1-1-2027", "31-12-2027")]
    [InlineData("2027-01-01", "2027-12-31")]
    [InlineData("  1 January 2027  ", "  31 December 2027  ")]
    public async Task OnPostAsync_ReadsTheDatesInTheFormatsTheRevisionDatesFormAccepts(string from, string to)
    {
        var reports = ReportsReturning(new DiseaseReviewsReportDto());
        var model = CreatePage(reports);
        model.FromDate = from;
        model.ToDate = to;

        Assert.IsType<FileContentResult>(await model.OnPostAsync(CancellationToken.None));

        var requested = reports.RequestedDiseaseReviews!.Value;
        Assert.Equal(new DateTime(2027, 1, 1), requested.FromDate);
        Assert.Equal(new DateTime(2027, 12, 31), requested.ToDate);
    }

    [Fact]
    public async Task OnPostAsync_AcceptsTheEdgesOfTheSupportedDateRange()
    {
        var reports = ReportsReturning(new DiseaseReviewsReportDto());
        var model = CreatePage(reports);
        model.FromDate = "1 January 1900";
        model.ToDate = "6 June 2079";

        Assert.IsType<FileContentResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Equal(new DateTime(1900, 1, 1), reports.RequestedDiseaseReviews!.Value.FromDate);
        Assert.Equal(new DateTime(2079, 6, 6), reports.RequestedDiseaseReviews!.Value.ToDate);
    }

    [Theory]
    [InlineData("", "31 December 2027", "Enter a from date", null)]
    [InlineData("   ", "31 December 2027", "Enter a from date", null)]
    [InlineData(null, "31 December 2027", "Enter a from date", null)]
    [InlineData("not a date", "31 December 2027", "The from date must be a real date, for example 1 January 2027", null)]
    [InlineData("31 February 2027", "31 December 2027", "The from date must be a real date, for example 1 January 2027", null)]
    [InlineData("31 December 1899", "31 December 2027", "The from date must be between 1 January 1900 and 6 June 2079", null)]
    [InlineData("1 January 2027", "", null, "Enter a to date")]
    [InlineData("1 January 2027", "soon", null, "The to date must be a real date, for example 1 January 2027")]
    [InlineData("1 January 2027", "7 June 2079", null, "The to date must be between 1 January 1900 and 6 June 2079")]
    public async Task OnPostAsync_ShowsWhyADateCannotBeUsed_AndDoesNotAskForTheReport(string? from, string? to, string? fromError, string? toError)
    {
        var reports = ReportsReturning(new DiseaseReviewsReportDto());
        var model = CreatePage(reports);
        model.FromDate = from;
        model.ToDate = to;

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(reports.RequestedDiseaseReviews);
        string[] expectedFromErrors = fromError is null ? [] : [fromError];
        string[] expectedToErrors = toError is null ? [] : [toError];
        Assert.Equal(expectedFromErrors, ErrorsFor(model, nameof(DiseaseReviewsModel.FromDate)));
        Assert.Equal(expectedToErrors, ErrorsFor(model, nameof(DiseaseReviewsModel.ToDate)));
        Assert.Equal(from, model.FromDate);
        Assert.Equal(to, model.ToDate);
        Assert.Equal([Jane, Sam], model.Authors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnPostAsync_RequiresAnAuthorFromTheList(bool unknownAuthor)
    {
        var reports = ReportsReturning(new DiseaseReviewsReportDto());
        var model = CreatePage(reports);
        model.AuthorId = unknownAuthor ? Guid.NewGuid() : null;

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(reports.RequestedDiseaseReviews);
        Assert.Equal(["Select an author"], ErrorsFor(model, nameof(DiseaseReviewsModel.AuthorId)));
    }

    [Fact]
    public async Task OnPostAsync_ShowsEveryProblemAtOnce()
    {
        var model = CreatePage();
        model.FromDate = string.Empty;
        model.ToDate = "someday";
        model.AuthorId = null;
        model.Description = new string('a', DiseaseReviewsModel.MaxDescriptionLength + 1);

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Equal(4, model.ModelState.ErrorCount);
    }

    [Fact]
    public async Task OnPostAsync_AcceptsADescriptionOfTheLongestLength_AndRejectsOneCharacterMore()
    {
        var model = CreatePage();
        model.Description = new string('a', DiseaseReviewsModel.MaxDescriptionLength);
        Assert.IsType<FileContentResult>(await model.OnPostAsync(CancellationToken.None));

        model = CreatePage();
        model.Description = new string('a', DiseaseReviewsModel.MaxDescriptionLength + 1);

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));
        Assert.Equal(
            [$"Description must be {DiseaseReviewsModel.MaxDescriptionLength} characters or fewer"],
            ErrorsFor(model, nameof(DiseaseReviewsModel.Description)));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Annual review  ", "Annual review")]
    public async Task OnPostAsync_PrintsTheDescriptionTrimmed_AndAcceptsNone(string? description, string expected)
    {
        var renderer = new FakeViewRenderer("<html/>");
        var model = CreatePage(renderer: renderer);
        model.Description = description;

        Assert.IsType<FileContentResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Equal(expected, Assert.IsType<DiseaseReviewsReportViewModel>(renderer.Model).Description);
    }

    [Fact]
    public async Task OnPostAsync_IgnoresTheModelBindersOwnErrors_BecauseItChecksEveryFieldItself()
    {
        var model = CreatePage();
        model.ModelState.AddModelError(nameof(DiseaseReviewsModel.AuthorId), "The value 'x' is not valid for AuthorId.");

        Assert.IsType<FileContentResult>(await model.OnPostAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnPostAsync_ShowsAnErrorAndDoesNotAskForTheReport_WhenTheAuthorsCannotBeLoaded()
    {
        var reports = ReportsReturning(new DiseaseReviewsReportDto());
        var model = CreatePage(reports, users: new StubGlobalUsersApiService([], new HttpRequestException("boom")));

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Null(reports.RequestedDiseaseReviews);
        Assert.Equal([DiseaseReviewsModel.AuthorsLoadFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Equal(1, model.ModelState.ErrorCount);
    }

    [Fact]
    public async Task OnPostAsync_Returns403_WhenTheApiRefusesTheUser()
    {
        var logger = new AlwaysEnabledLogger<DiseaseReviewsModel>();
        var reports = new FakeProfileReportsApiService(diseaseReviews: new DiseaseReviewsReportResult(DiseaseReviewsReportOutcome.Forbidden));

        var result = await CreatePage(reports, logger: logger).OnPostAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Contains(logger.Messages, message => message.Contains("refused the disease reviews report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostAsync_ShowsAnError_WhenTheApiFails()
    {
        var reports = new FakeProfileReportsApiService(diseaseReviews: new DiseaseReviewsReportResult(DiseaseReviewsReportOutcome.Error));
        var model = CreatePage(reports);

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Equal([DiseaseReviewsModel.ReportFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Equal([Jane, Sam], model.Authors);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnPostAsync_ShowsAnError_WhenTheApiCallThrows(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<DiseaseReviewsModel>();
        var reports = new FakeProfileReportsApiService(throwOnDiseaseReviews: (Exception)Activator.CreateInstance(exceptionType)!);
        var model = CreatePage(reports, logger: logger);

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Equal([DiseaseReviewsModel.ReportFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Contains(logger.Messages, message => message.Contains("disease reviews report data", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnPostAsync_ShowsAnError_WhenRenderingFails(bool failInViewRenderer)
    {
        var logger = new AlwaysEnabledLogger<DiseaseReviewsModel>();
        var renderer = new FakeViewRenderer("<html/>", failInViewRenderer ? new InvalidOperationException("view missing") : null);
        var pdf = new FakePdfService(PdfBytes, failInViewRenderer ? null : new PdfRenderException("no browser"));
        var model = CreatePage(renderer: renderer, pdf: pdf, logger: logger);

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));

        Assert.Equal([DiseaseReviewsModel.ReportFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Contains(logger.Messages, message => message.Contains("render the disease reviews report PDF", StringComparison.Ordinal));
    }

    private static string[] ErrorsFor(DiseaseReviewsModel model, string key) =>
        model.ModelState.TryGetValue(key, out var entry) ? [.. entry.Errors.Select(error => error.ErrorMessage)] : [];

    private static FakeProfileReportsApiService ReportsReturning(DiseaseReviewsReportDto report) =>
        new(diseaseReviews: new DiseaseReviewsReportResult(DiseaseReviewsReportOutcome.Success, report));

    private static DiseaseReviewsModel CreatePage(
        FakeProfileReportsApiService? reports = null,
        StubGlobalUsersApiService? users = null,
        FakeViewRenderer? renderer = null,
        FakePdfService? pdf = null,
        AlwaysEnabledLogger<DiseaseReviewsModel>? logger = null) =>
        new(
            users ?? new StubGlobalUsersApiService([Jane, Sam]),
            reports ?? ReportsReturning(new DiseaseReviewsReportDto()),
            renderer ?? new FakeViewRenderer("<html/>"),
            pdf ?? new FakePdfService(PdfBytes),
            logger ?? new AlwaysEnabledLogger<DiseaseReviewsModel>(),
            new FixedTimeProvider(ProducedAt))
        {
            FromDate = "1 January 2027",
            ToDate = "31 December 2027",
            AuthorId = Jane.Id,
            Description = "<i>Quarterly</i> check",
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
