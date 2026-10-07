using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.HelpSupport;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Pages.HelpSupport;

public class QualityStatementModelTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid StaticReportId = Guid.NewGuid();

    private static QualityStatementModel CreatePageModel(
        IReadOnlyList<StaticReportListItemDto>? staticReports = null,
        Exception? throwOnGetCurrentStaticReports = null,
        string? baseUrl = "https://api.example.test")
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new QualityStatementModel(
            new FakeApiClient(staticReports: staticReports, throwOnGetCurrentStaticReports: throwOnGetCurrentStaticReports),
            Options.Create(new ApiOptions { BaseUrl = baseUrl ?? string.Empty }),
            NullLogger<QualityStatementModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };
    }

    private static StaticReportListItemDto Report(string title) => new()
    {
        Id = VersionId,
        StaticReportId = StaticReportId,
        Title = title,
        VersionMajor = 1,
        EffectiveDateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EffectiveDateTo = null,
        IsUserManual = true,
        IsPublic = false,
        FileSize = 1024
    };

    [Fact]
    public async Task OnGetAsync_RedirectsToDocumentUrl_WhenCurrentVersionExists()
    {
        var pageModel = CreatePageModel([Report("D2R2 Quality Statement")]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"https://api.example.test/api/static-reports/versions/{VersionId}/document", redirect.Url);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenNoMatchingReportExists()
    {
        var pageModel = CreatePageModel([Report("Some Other Report")]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_RendersErrorBanner_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(throwOnGetCurrentStaticReports: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Equal("We could not load the quality statement. Try again later.", pageModel.ErrorMessage);
    }
}
