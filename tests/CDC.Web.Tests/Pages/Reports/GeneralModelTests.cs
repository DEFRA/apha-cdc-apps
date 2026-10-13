using System.Security.Claims;
using System.Text;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.Reports;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Pages.Reports;

public class GeneralModelTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid ReportId = Guid.NewGuid();

    private sealed class StubAuthorizationService(bool succeed) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(succeed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(succeed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private static GeneralModel CreatePageModel(
        bool canManage = true,
        IReadOnlyList<StaticReportListItemDto>? reports = null,
        IReadOnlyList<StaticReportListItemDto>? history = null,
        Exception? throwOnGetReports = null,
        bool operationResult = false,
        Exception? throwOnOperation = null)
    {
        var metadataProvider = new EmptyModelMetadataProvider();

        return new GeneralModel(
            new FakeApiClient(
                staticReports: reports,
                throwOnGetCurrentStaticReports: throwOnGetReports,
                staticReportHistory: history,
                staticReportOperationResult: operationResult,
                throwOnStaticReportOperation: throwOnOperation),
            new StubAuthorizationService(canManage),
            Options.Create(new ApiOptions { BaseUrl = "https://api.example.test/" }),
            new AlwaysEnabledLogger<GeneralModel>())
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(metadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = metadataProvider
        };
    }

    private static StaticReportListItemDto Report(string title, bool isPublic = false, bool isUserManual = false) => new()
    {
        Id = VersionId,
        StaticReportId = ReportId,
        Title = title,
        VersionMajor = 1,
        EffectiveDateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        IsUserManual = isUserManual,
        IsPublic = isPublic,
        FileSize = 1024
    };

    private static FormFile File(string name, byte[] content) =>
        new(new MemoryStream(content), 0, content.Length, "ReportFile", name);

    [Fact]
    public async Task OnGetAsync_LoadsCurrentReports_WhenNotHistoryPage()
    {
        var model = CreatePageModel(reports: [Report("A"), Report("B")]);

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, model.PagedReports.Count);
        Assert.Equal("General reports", model.ReportTitle);
        Assert.True(model.CanManageReports);
    }

    [Fact]
    public async Task OnGetAsync_UsesFirstHistoryTitle_WhenHistoryPage()
    {
        var model = CreatePageModel(history: [Report("History title")]);
        model.StaticReportId = ReportId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.IsHistoryPage);
        Assert.Equal("History title", model.ReportTitle);
    }

    [Fact]
    public async Task OnGetAsync_UsesDefaultTitle_WhenHistoryIsEmpty()
    {
        var model = CreatePageModel(history: []);
        model.StaticReportId = ReportId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal("General reports", model.ReportTitle);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsEmptyReports_WhenApiThrows()
    {
        var model = CreatePageModel(throwOnGetReports: new HttpRequestException("down"));

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.Reports);
    }

    [Fact]
    public async Task OnGetAsync_HidesUpload_WhenUserCannotManage()
    {
        var model = CreatePageModel(canManage: false);
        model.ShowUpload = true;

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.ShowUpload);
    }

    [Theory]
    [InlineData("All", 25, 1, 25)]
    [InlineData("10", 25, 3, 10)]
    [InlineData("bad", 15, 2, 10)]
    public async Task OnGetAsync_AppliesPageSize(string pageSize, int count, int expectedPages, int expectedPaged)
    {
        var model = CreatePageModel(reports: [.. Enumerable.Range(1, count).Select(i => Report($"R{i}"))]);
        model.PageSize = pageSize;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedPages, model.TotalPages);
        Assert.Equal(expectedPaged, model.PagedReports.Count);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReturnsForbid_WhenUserCannotManage()
    {
        var model = CreatePageModel(canManage: false);

        Assert.IsType<ForbidResult>(await model.OnPostDeleteAsync(VersionId, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostDeleteAsync_Redirects_WhenIdIsEmpty()
    {
        var model = CreatePageModel();

        var result = await model.OnPostDeleteAsync(Guid.Empty, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(model.StatusMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_SetsStatusMessage_WhenDeleted()
    {
        var model = CreatePageModel(operationResult: true);

        await model.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.Equal("The report version was successfully deleted.", model.StatusMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_SetsErrorMessage_WhenDeleteRejected()
    {
        var model = CreatePageModel(operationResult: false);

        await model.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.Equal("The report version could not be deleted.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_SetsErrorMessage_WhenApiThrows()
    {
        var model = CreatePageModel(throwOnOperation: new HttpRequestException("down"));

        await model.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.Equal("The report version could not be deleted.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostMakePublicAsync_ReturnsForbid_WhenUserCannotManage()
    {
        var model = CreatePageModel(canManage: false);

        Assert.IsType<ForbidResult>(await model.OnPostMakePublicAsync(VersionId, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostMakePublicAsync_Redirects_WhenIdIsEmpty()
    {
        var model = CreatePageModel();

        var result = await model.OnPostMakePublicAsync(Guid.Empty, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(model.StatusMessage);
    }

    [Fact]
    public async Task OnPostMakePublicAsync_SetsStatusMessage_WhenSuccessful()
    {
        var model = CreatePageModel(operationResult: true);

        await model.OnPostMakePublicAsync(VersionId, CancellationToken.None);

        Assert.Equal("The report version was made public.", model.StatusMessage);
    }

    [Fact]
    public async Task OnPostMakePublicAsync_SetsErrorMessage_WhenRejected()
    {
        var model = CreatePageModel(operationResult: false);

        await model.OnPostMakePublicAsync(VersionId, CancellationToken.None);

        Assert.Equal("The report version could not be made public.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostMakePublicAsync_SetsErrorMessage_WhenApiThrows()
    {
        var model = CreatePageModel(throwOnOperation: new HttpRequestException("down"));

        await model.OnPostMakePublicAsync(VersionId, CancellationToken.None);

        Assert.Equal("The report version could not be made public.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostUploadAsync_ReturnsForbid_WhenUserCannotManage()
    {
        var model = CreatePageModel(canManage: false);

        Assert.IsType<ForbidResult>(await model.OnPostUploadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(null, "Choose a file to upload")]
    [InlineData("empty.pdf", "Choose a file to upload")]
    [InlineData("notes.txt", "Choose a PDF file to upload")]
    [InlineData(".pdf", "Choose a PDF file to upload")]
    public async Task OnPostUploadAsync_ShowsError_WhenFileIsInvalid(string? fileName, string expectedError)
    {
        var model = CreatePageModel();
        if (fileName is not null)
        {
            model.ReportFile = File(fileName, fileName == "empty.pdf" ? [] : [1, 2, 3]);
        }

        var result = await model.OnPostUploadAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(expectedError, model.UploadError);
        Assert.True(model.ShowUpload);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenFileIsTooLarge()
    {
        var model = CreatePageModel();
        model.ReportFile = new FormFile(new MemoryStream(), 0, 20 * 1024 * 1024 + 1, "ReportFile", "big.pdf");

        await model.OnPostUploadAsync(CancellationToken.None);

        Assert.Equal("The file must be smaller than 20MB", model.UploadError);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenContentIsNotPdf()
    {
        var model = CreatePageModel();
        model.ReportFile = File("fake.pdf", Encoding.ASCII.GetBytes("not a pdf"));

        await model.OnPostUploadAsync(CancellationToken.None);

        Assert.Equal("Choose a PDF file to upload", model.UploadError);
    }

    [Fact]
    public async Task OnPostUploadAsync_RedirectsWithStatus_WhenUploadSucceeds()
    {
        var model = CreatePageModel(operationResult: true);
        model.ReportFile = File("report.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 data"));

        var result = await model.OnPostUploadAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("The report was uploaded successfully.", model.StatusMessage);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsError_WhenUploadReturnsFalse()
    {
        var model = CreatePageModel(operationResult: false);
        model.ReportFile = File("report.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 data"));

        var result = await model.OnPostUploadAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Failed to upload the report. Try again.", model.UploadError);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsRejectionMessage_WhenApiRejectsUpload()
    {
        var model = CreatePageModel(throwOnOperation: new StaticReportUploadRejectedException("Duplicate title"));
        model.ReportFile = File("report.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 data"));

        await model.OnPostUploadAsync(CancellationToken.None);

        Assert.Equal("Duplicate title", model.UploadError);
    }

    [Fact]
    public async Task OnPostUploadAsync_ShowsGenericError_WhenApiThrows()
    {
        var model = CreatePageModel(throwOnOperation: new HttpRequestException("down"));
        model.ReportFile = File("report.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 data"));

        await model.OnPostUploadAsync(CancellationToken.None);

        Assert.Equal("Failed to upload the report. Try again.", model.UploadError);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public async Task CanMakePublic_ReflectsPermissionsAndReportFlags(bool canManage, bool isPublic, bool isUserManual, bool expected)
    {
        var model = CreatePageModel(canManage);
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(expected, model.CanMakePublic(Report("A", isPublic, isUserManual)));
    }

    [Fact]
    public void GetDocumentUrl_TrimsTrailingSlashFromBaseUrl()
    {
        var model = CreatePageModel();

        Assert.Equal(
            $"https://api.example.test/api/static-reports/versions/{VersionId}/document",
            model.GetDocumentUrl(VersionId));
    }

    [Fact]
    public void GetDocumentUrl_UsesRelativePath_WhenBaseUrlIsNull()
    {
        var model = new GeneralModel(
            new FakeApiClient(),
            new StubAuthorizationService(true),
            Options.Create(new ApiOptions { BaseUrl = null! }),
            new AlwaysEnabledLogger<GeneralModel>());

        Assert.Equal($"/api/static-reports/versions/{VersionId}/document", model.GetDocumentUrl(VersionId));
    }

    [Theory]
    [InlineData("abc", 10)]
    [InlineData("0", 10)]
    [InlineData("-5", 10)]
    [InlineData("5", 5)]
    public async Task OnGetAsync_ResolvesPageSize(string pageSize, int expectedItems)
    {
        var reports = Enumerable.Range(1, 12).Select(index => Report($"Report {index}")).ToList();
        var model = CreatePageModel(reports: reports);
        model.PageSize = pageSize;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedItems, model.PagedReports.Count);
    }

    [Fact]
    public void BuildPageUrl_And_BuildHistoryUrl_UseThePageUrlHelper()
    {
        var model = CreatePageModel();
        model.Url = new StubUrlHelper("/Reports/General?x=1");

        Assert.Equal("/Reports/General?x=1", model.BuildPageUrl(2));
        Assert.Equal("/Reports/General?x=1", model.BuildHistoryUrl(ReportId));
    }

    private sealed class StubUrlHelper(string result) : Microsoft.AspNetCore.Mvc.IUrlHelper
    {
        public ActionContext ActionContext { get; } = new() { RouteData = new Microsoft.AspNetCore.Routing.RouteData() };

        public string? Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext) => result;

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => result;

        public string? RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext) => result;
    }

    [Theory]
    [InlineData(false, "01/01/2024 -")]
    [InlineData(true, "01/01/2024 - 31/12/2024")]
    public void FormatEffectiveDates_FormatsOpenAndClosedRanges(bool closed, string expected)
    {
        var report = Report("A") with { EffectiveDateTo = closed ? new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc) : null };

        Assert.Equal(expected, GeneralModel.FormatEffectiveDates(report));
    }
}
