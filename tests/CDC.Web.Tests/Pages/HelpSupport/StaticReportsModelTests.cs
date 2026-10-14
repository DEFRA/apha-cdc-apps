using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.HelpSupport;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Pages.HelpSupport;

public class StaticReportsModelTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid StaticReportId = Guid.NewGuid();

    private static StaticReportsModel CreatePageModel(
        IReadOnlyList<StaticReportListItemDto>? staticReports = null,
        Exception? throwOnGetCurrentStaticReports = null,
        IReadOnlyList<StaticReportListItemDto>? staticReportHistory = null,
        Exception? throwOnGetStaticReportHistory = null,
        bool canUploadStaticReports = true,
        UploadStaticReportResult? uploadStaticReportResult = null,
        DeleteStaticReportVersionResult? deleteStaticReportVersionResult = null,
        string? baseUrl = "https://api.example.test")
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new StaticReportsModel(
            new FakeApiClient(
                staticReports: staticReports,
                throwOnGetCurrentStaticReports: throwOnGetCurrentStaticReports,
                staticReportHistory: staticReportHistory,
                throwOnGetStaticReportHistory: throwOnGetStaticReportHistory,
                canUploadStaticReports: canUploadStaticReports,
                uploadStaticReportResult: uploadStaticReportResult,
                deleteStaticReportVersionResult: deleteStaticReportVersionResult),
            Options.Create(new ApiOptions { BaseUrl = baseUrl ?? string.Empty }),
            new AlwaysEnabledLogger<StaticReportsModel>())
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider,
            Url = new FakeUrlHelper(values => $"/HelpSupport/StaticReports?PageNumber={values["PageNumber"]}")
        };
    }

    private static FormFile CreateFormFile(string fileName, string contentType, byte[]? content = null)
    {
        var bytes = content ?? [1, 2, 3];
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "UploadedFile", fileName)
        {
            Headers = new HeaderDictionary()
        };
        formFile.ContentType = contentType;

        return formFile;
    }

    private static StaticReportListItemDto Report(string title, DateTime? effectiveDateTo = null) => new()
    {
        Id = VersionId,
        StaticReportId = StaticReportId,
        Title = title,
        VersionMajor = 1,
        EffectiveDateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EffectiveDateTo = effectiveDateTo,
        IsUserManual = false,
        IsPublic = true,
        FileSize = 1024
    };


    [Fact]
    public async Task OnGetAsync_PopulatesReports_WhenTheApiSucceeds()
    {
        var pageModel = CreatePageModel([Report("Report A"), Report("Report B")]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, pageModel.TotalResultCount);
        Assert.Equal(2, pageModel.PagedReports.Count);
        Assert.Equal("Static reports", pageModel.Breadcrumb!.PageName);
    }

    [Fact]
    public async Task OnGetAsync_UsesUserManualBreadcrumb_WhenUserManualIsSet()
    {
        var pageModel = CreatePageModel([]);
        pageModel.UserManual = 1;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.IsUserManual);
        Assert.Equal("Help Using D2R2", pageModel.Breadcrumb!.PageName);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsEmptyReports_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(throwOnGetCurrentStaticReports: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Empty(pageModel.Reports);
        Assert.Empty(pageModel.PagedReports);
        Assert.Equal(0, pageModel.TotalResultCount);
    }

    [Fact]
    public async Task OnGetAsync_ShowsAllReportsOnOnePage_WhenPageSizeIsAll()
    {
        var pageModel = CreatePageModel(Enumerable.Range(1, 12).Select(i => Report($"Report {i}")).ToList());
        pageModel.PageSize = "All";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalPages);
        Assert.Equal(12, pageModel.PagedReports.Count);
    }

    [Fact]
    public async Task OnGetAsync_PagesResults_WhenPageSizeIsNumeric()
    {
        var pageModel = CreatePageModel(Enumerable.Range(1, 25).Select(i => Report($"Report {i}")).ToList());
        pageModel.PageSize = "10";
        pageModel.PageNumber = 2;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, pageModel.TotalPages);
        Assert.Equal(10, pageModel.PagedReports.Count);
    }

    [Fact]
    public async Task OnGetAsync_ClampsPageNumber_WhenRequestedPageIsBeyondTheLastPage()
    {
        var pageModel = CreatePageModel([Report("Report A")]);
        pageModel.PageSize = "10";
        pageModel.PageNumber = 99;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.PageNumber);
    }

    [Fact]
    public async Task OnGetAsync_DefaultsToPageSizeTen_WhenPageSizeIsUnrecognised()
    {
        var pageModel = CreatePageModel(Enumerable.Range(1, 15).Select(i => Report($"Report {i}")).ToList());
        pageModel.PageSize = "not-a-number";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(10, pageModel.PagedReports.Count);
    }

    [Fact]
    public void GetDocumentUrl_BuildsDownloadLink_FromApiBaseUrl()
    {
        var pageModel = CreatePageModel(baseUrl: "https://api.example.test/");

        var url = pageModel.GetDocumentUrl(VersionId);

        Assert.Equal($"https://api.example.test/api/static-reports/versions/{VersionId}/document", url);
    }

    [Fact]
    public void BuildPageUrl_DelegatesToUrlHelper()
    {
        var pageModel = CreatePageModel();
        pageModel.Url = new FakeUrlHelper(values => $"/HelpSupport/StaticReports?PageNumber={values["PageNumber"]}");

        var url = pageModel.BuildPageUrl(3);

        Assert.Equal("/HelpSupport/StaticReports?PageNumber=3", url);
    }

    [Fact]
    public void FormatEffectiveDates_ShowsOpenRange_WhenVersionIsCurrent()
    {
        var report = Report("Report A");

        Assert.Equal("01/01/2024 -", StaticReportsModel.FormatEffectiveDates(report));
    }

    [Fact]
    public void FormatEffectiveDates_ShowsClosedRange_WhenVersionIsSuperseded()
    {
        var report = Report("Report A", new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("01/01/2024 - 01/06/2024", StaticReportsModel.FormatEffectiveDates(report));
    }

    [Fact]
    public async Task OnGetAsync_LoadsHistory_WhenStaticReportIdIsSet()
    {
        var pageModel = CreatePageModel(staticReportHistory: [Report("Help using D2R2") with { IsUserManual = true }]);
        pageModel.StaticReportId = StaticReportId;
        pageModel.UserManual = 1;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.IsHistoryMode);
        Assert.Equal("History for Help using D2R2", pageModel.Breadcrumb!.PageName);
        Assert.Equal("Help Using D2R2", pageModel.Breadcrumb.ParentPageName);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsEmptyHistory_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(throwOnGetStaticReportHistory: new HttpRequestException("connection refused"));
        pageModel.StaticReportId = StaticReportId;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.Reports);
        Assert.Equal("History", pageModel.Breadcrumb!.PageName);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenNoFileIsChosen()
    {
        var pageModel = CreatePageModel();
        pageModel.UploadedFile = null;

        await pageModel.OnPostUploadAsync(CancellationToken.None);

        Assert.Contains("Please choose a file to upload", pageModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenFileIsNotAPdf()
    {
        var pageModel = CreatePageModel();
        pageModel.UploadedFile = CreateFormFile("manual.docx", "application/msword");

        await pageModel.OnPostUploadAsync(CancellationToken.None);

        Assert.Contains("must be a Pdf", pageModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenFileNameIsTooShort()
    {
        var pageModel = CreatePageModel();
        pageModel.UploadedFile = CreateFormFile("a.pd", "application/pdf");

        await pageModel.OnPostUploadAsync(CancellationToken.None);

        Assert.Contains("filename was less than 5 characters long", pageModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsSuccess_WhenTheApiSucceeds()
    {
        var pageModel = CreatePageModel(uploadStaticReportResult: new UploadStaticReportResult(UploadStaticReportOutcome.Success, null));
        pageModel.UploadedFile = CreateFormFile("Help using D2R2.pdf", "application/pdf");
        pageModel.MakePublic = true;

        var result = await pageModel.OnPostUploadAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("The report was uploaded successfully", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenTheApiReturnsForbidden()
    {
        var pageModel = CreatePageModel(
            uploadStaticReportResult: new UploadStaticReportResult(UploadStaticReportOutcome.Forbidden, "Not permitted"));
        pageModel.UploadedFile = CreateFormFile("Help using D2R2.pdf", "application/pdf");

        await pageModel.OnPostUploadAsync(CancellationToken.None);

        Assert.Contains("Not permitted", pageModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ShowsSuccess_WhenTheApiSucceeds()
    {
        var pageModel = CreatePageModel(
            deleteStaticReportVersionResult: new DeleteStaticReportVersionResult(DeleteStaticReportVersionOutcome.Success, null));

        var result = await pageModel.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("The report version was successfully deleted", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ShowsError_WhenTheApiFails()
    {
        var pageModel = CreatePageModel(
            deleteStaticReportVersionResult: new DeleteStaticReportVersionResult(DeleteStaticReportVersionOutcome.Forbidden, "Not permitted"));

        await pageModel.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.Contains("Not permitted", pageModel.ErrorMessage);
    }

    /// <summary>Minimal <see cref="IUrlHelper"/> double: only <see cref="RouteUrl"/> is used by
    /// <see cref="StaticReportsModel.BuildPageUrl"/> (via the <c>Url.Page</c> extension).</summary>
    private sealed class FakeUrlHelper(Func<RouteValueDictionary, string?> routeUrl) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new(
            new DefaultHttpContext(),
            new RouteData(),
            new PageActionDescriptor());

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => throw new NotSupportedException();

        public bool IsLocalUrl(string? url) => throw new NotSupportedException();

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext) => routeUrl(new RouteValueDictionary(routeContext.Values));
    }
}
