using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ViewContributionsReportModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();

    [Fact]
    public async Task OnGetAsync_ReturnsThePdfAsAnAttachmentNamedAfterTheProfile()
    {
        var reports = new FakeProfileReportsApiService(contributions: new ContributionsReportResult(ContributionsReportOutcome.Success, CreateReport()));
        var renderer = new FakeViewRenderer("<html>report</html>");
        var pdf = new FakePdfService(PdfBytes);

        var result = await CreatePageModel(reports, renderer, pdf).OnGetAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(PdfBytes, file.FileContents);
        Assert.Equal("Anthrax_Contributions.pdf", file.FileDownloadName);
        Assert.Equal(ProfileId, reports.RequestedProfileId);
        Assert.Equal(ViewContributionsReportModel.ReportViewPath, renderer.ViewPath);
        Assert.Equal("<html>report</html>", pdf.Html);
        Assert.Contains("Anthrax", pdf.Options!.HeaderHtml);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileHasNoDraftVersions()
    {
        var model = CreatePageModel(new FakeProfileReportsApiService(contributions: new ContributionsReportResult(ContributionsReportOutcome.NotFound)));

        Assert.IsType<NotFoundResult>(await model.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_Returns403_WhenTheApiRefusesTheUser()
    {
        var model = CreatePageModel(new FakeProfileReportsApiService(contributions: new ContributionsReportResult(ContributionsReportOutcome.Forbidden)));

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheApiFails()
    {
        var model = CreatePageModel(new FakeProfileReportsApiService(contributions: new ContributionsReportResult(ContributionsReportOutcome.Error)));

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewContributionsReportModel.FailureMessage, model.ErrorMessage);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenTheApiCallThrows(Type exceptionType)
    {
        var reports = new FakeProfileReportsApiService(throwOnContributions: (Exception)Activator.CreateInstance(exceptionType)!);
        var model = CreatePageModel(reports);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewContributionsReportModel.FailureMessage, model.ErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnGetAsync_ShowsTheErrorPage_WhenRenderingFails(bool failInViewRenderer)
    {
        var reports = new FakeProfileReportsApiService(contributions: new ContributionsReportResult(ContributionsReportOutcome.Success, CreateReport()));
        var renderer = new FakeViewRenderer("<html/>", failInViewRenderer ? new InvalidOperationException("view missing") : null);
        var pdf = new FakePdfService(PdfBytes, failInViewRenderer ? null : new PdfRenderException("no browser"));
        var model = CreatePageModel(reports, renderer, pdf);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(ViewContributionsReportModel.FailureMessage, model.ErrorMessage);
    }

    private static ContributionsReportDto CreateReport() => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Anthrax",
        Versions =
        [
            new ContributionsReportVersionDto { FullTitle = "Anthrax", VersionMajor = 1, EffectiveDateFrom = new DateTime(2026, 1, 1) }
        ]
    };

    private static ViewContributionsReportModel CreatePageModel(
        FakeProfileReportsApiService reports,
        FakeViewRenderer? renderer = null,
        FakePdfService? pdf = null) =>
        new(reports, renderer ?? new FakeViewRenderer("<html/>"), pdf ?? new FakePdfService(PdfBytes), NullLogger<ViewContributionsReportModel>.Instance)
        {
            ProfileId = ProfileId,
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class FakeViewRenderer(string html, Exception? throwOnRender = null) : IRazorViewRenderer
    {
        public string? ViewPath { get; private set; }

        public Task<string> RenderAsync<TModel>(string viewPath, TModel model)
        {
            ViewPath = viewPath;

            return throwOnRender is not null ? throw throwOnRender : Task.FromResult(html);
        }
    }

    private sealed class FakePdfService(byte[] pdf, Exception? throwOnRender = null) : IPlaywrightPdfService
    {
        public string? Html { get; private set; }

        public PdfRenderOptions? Options { get; private set; }

        public Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
        {
            Html = html;
            Options = options;

            return throwOnRender is not null ? throw throwOnRender : Task.FromResult(pdf);
        }
    }
}
