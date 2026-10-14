using System.Security.Claims;
using CDC.Common.Contracts;
using CDC.Web.Authorization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ProfileReportsModelTests
{
    private const string Summary = "SummaryProfileGUID";
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid StoredReportId = Guid.NewGuid();
    private static readonly Guid PendingReportId = Guid.NewGuid();
    private static readonly byte[] StoredPdf = "%PDF-1.7 stored"u8.ToArray();
    private static readonly byte[] GeneratedPdf = "%PDF-1.7 generated"u8.ToArray();

    [Fact]
    public async Task OnGetAsync_ShowsTheOverview_WithTheLegacyTitleAndPerRowActions()
    {
        var model = CreatePageModel(CreateApi(), new FakeGenerator(Summary), AuthorizationRoles.PolicyProfileUser);

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Profile reports for Anthrax (What if) v1.2", model.PageTitle);
        Assert.True(model.ShowIntroText);
        Assert.True(model.CanCreateBespokeReport);
        Assert.Collection(
            model.Rows,
            stored =>
            {
                Assert.Equal("Summary profile", stored.DisplayName);
                Assert.True(stored.CanDownload);
                Assert.True(stored.CanRegenerate);
            },
            pending =>
            {
                Assert.Equal("QA brief", pending.DisplayName);
                Assert.False(pending.CanDownload);
                Assert.False(pending.CanRegenerate);
            });
    }

    [Fact]
    public async Task OnGetAsync_HidesTheIntroText_ForAProfileEditor()
    {
        var model = CreatePageModel(CreateApi(), new FakeGenerator(Summary), AuthorizationRoles.ProfileEditor);

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.ShowIntroText);
    }

    [Fact]
    public async Task OnGetAsync_OffersNoRegeneration_WhenTheUserMayNotCreateReports()
    {
        var model = CreatePageModel(CreateApi(canCreate: false), new FakeGenerator(Summary), AuthorizationRoles.Contributor);

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.CanCreateBespokeReport);
        Assert.All(model.Rows, row => Assert.False(row.CanRegenerate));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        var api = new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.NotFound));

        Assert.IsType<NotFoundResult>(await CreatePageModel(api).OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_Returns403_WhenTheApiRefusesTheUser()
    {
        var api = new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Forbidden));

        var result = await CreatePageModel(api).OnGetAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheErrorMessage_WhenTheApiFails()
    {
        var api = new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Error));
        var model = CreatePageModel(api);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(ProfileReportsModel.LoadFailureMessage, model.ErrorMessage);
        Assert.Null(model.Overview);
        Assert.Equal("Profile reports", model.PageTitle);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsTheErrorMessage_WhenTheApiCallThrows(Type exceptionType)
    {
        var api = new FakeProfileReportsApiService(throwOnOverview: (Exception)Activator.CreateInstance(exceptionType)!);
        var model = CreatePageModel(api);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(ProfileReportsModel.LoadFailureMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsTheStoredPdf_NamedAfterTheProfileAndReport()
    {
        var model = CreatePageModel(CreateApi());

        var result = await model.OnGetDownloadAsync(StoredReportId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(StoredPdf, file.FileContents);
        Assert.Equal("Anthrax (What if)_Summary profile.pdf", file.FileDownloadName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnGetDownloadAsync_ReturnsNotFound_WhenThereIsNothingToDownload(bool unknownReport)
    {
        var model = CreatePageModel(CreateApi());

        var result = await model.OnGetDownloadAsync(unknownReport ? Guid.NewGuid() : PendingReportId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsNotFound_WhenTheStoredPdfHasGone()
    {
        var model = CreatePageModel(CreateApi(hasStoredPdf: false));

        Assert.IsType<NotFoundResult>(await model.OnGetDownloadAsync(StoredReportId, CancellationToken.None));
    }

    [Fact]
    public async Task OnGetDownloadAsync_ShowsTheErrorMessage_WhenTheDownloadFails()
    {
        var api = CreateApi(throwOnStoredReport: new HttpRequestException("boom"));
        var model = CreatePageModel(api);

        Assert.IsType<PageResult>(await model.OnGetDownloadAsync(StoredReportId, CancellationToken.None));
        Assert.Equal(ProfileReportsModel.DownloadFailureMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsTheLoadFailure_WhenTheOverviewCannotBeLoaded()
    {
        var api = new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.NotFound));

        Assert.IsType<NotFoundResult>(await CreatePageModel(api).OnGetDownloadAsync(StoredReportId, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostRegenerateAsync_GeneratesAndStoresThePdf_ThenRedirectsWithASuccessMessage()
    {
        var api = CreateApi();
        var generator = new FakeGenerator(Summary);
        var model = CreatePageModel(api, generator);

        var result = await model.OnPostRegenerateAsync(StoredReportId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(ProfileVersionId, redirect.RouteValues!["profileVersionId"]);
        Assert.Equal("Summary profile: Report generation succeeded", model.SuccessMessage);
        Assert.Null(model.RegenerationErrorMessage);
        Assert.Equal((StoredReportId, Summary, GeneratedPdf), api.Saved);
        Assert.Equal(ProfileVersionId, generator.RequestedProfileVersionId);
    }

    [Fact]
    public async Task OnPostRegenerateAsync_ReturnsNotFound_ForAnUnknownReport()
    {
        var model = CreatePageModel(CreateApi(), new FakeGenerator(Summary));

        Assert.IsType<NotFoundResult>(await model.OnPostRegenerateAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task OnPostRegenerateAsync_ReturnsNotFound_WhenNoGeneratorExistsForTheReport()
    {
        var model = CreatePageModel(CreateApi(), new FakeGenerator(Summary));

        Assert.IsType<NotFoundResult>(await model.OnPostRegenerateAsync(PendingReportId, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostRegenerateAsync_Returns403_WhenTheUserMayNotCreateReports()
    {
        var api = CreateApi(canCreate: false);
        var model = CreatePageModel(api, new FakeGenerator(Summary));

        var result = await model.OnPostRegenerateAsync(StoredReportId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Null(api.Saved);
    }

    [Fact]
    public async Task OnPostRegenerateAsync_ReturnsTheLoadFailure_WhenTheOverviewCannotBeLoaded()
    {
        var api = new FakeProfileReportsApiService(throwOnOverview: new HttpRequestException("boom"));

        Assert.IsType<PageResult>(await CreatePageModel(api, new FakeGenerator(Summary)).OnPostRegenerateAsync(StoredReportId, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostRegenerateAsync_ReportsAFailure_WhenTheStoreIsRefused()
    {
        var api = CreateApi(saveOutcome: ProfileReportsOutcome.Forbidden);
        var model = CreatePageModel(api, new FakeGenerator(Summary));

        var result = await model.OnPostRegenerateAsync(StoredReportId, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Summary profile: Report generation failed", model.RegenerationErrorMessage);
        Assert.Null(model.SuccessMessage);
    }

    [Theory]
    [InlineData(typeof(PdfRenderException), true)]
    [InlineData(typeof(HttpRequestException), false)]
    [InlineData(typeof(InvalidOperationException), true)]
    public async Task OnPostRegenerateAsync_ReportsAFailure_WhenGeneratingOrStoringThrows(Type exceptionType, bool inGenerator)
    {
        var exception = exceptionType == typeof(PdfRenderException)
            ? new PdfRenderException("no browser")
            : (Exception)Activator.CreateInstance(exceptionType)!;
        var api = CreateApi(throwOnSave: inGenerator ? null : exception);
        var generator = new FakeGenerator(Summary) { ThrowOnGenerate = inGenerator ? exception : null };
        var model = CreatePageModel(api, generator);

        var result = await model.OnPostRegenerateAsync(StoredReportId, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Summary profile: Report generation failed", model.RegenerationErrorMessage);
    }

    private static FakeProfileReportsApiService CreateApi(
        bool canCreate = true,
        bool hasStoredPdf = true,
        ProfileReportsOutcome saveOutcome = ProfileReportsOutcome.Success,
        Exception? throwOnStoredReport = null,
        Exception? throwOnSave = null) =>
        new(
            overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Success, CreateOverview(canCreate)),
            storedReport: hasStoredPdf ? StoredPdf : null,
            saveOutcome: saveOutcome,
            throwOnStoredReport: throwOnStoredReport,
            throwOnSave: throwOnSave);

    private static ProfileReportsOverviewDto CreateOverview(bool canCreate) => new()
    {
        ProfileVersionId = ProfileVersionId,
        ProfileId = ProfileId,
        FullTitle = "Anthrax <em>(What if)</em>",
        VersionLabel = "1.2",
        IsCurrent = true,
        CanCreateReports = canCreate,
        Reports =
        [
            new ProfileReportListItemDto { Id = StoredReportId, ReportName = Summary, DisplayName = "Summary profile", HasPdfData = true, FileSize = 10 },
            new ProfileReportListItemDto { Id = PendingReportId, ReportName = "QABriefGUID", DisplayName = "QA brief" }
        ]
    };

    private static ProfileReportsModel CreatePageModel(
        FakeProfileReportsApiService api,
        FakeGenerator? generator = null,
        string role = AuthorizationRoles.ProfileEditor)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"))
        };

        return new ProfileReportsModel(api, generator ?? new FakeGenerator(), CreateNavigation(), NullLogger<ProfileReportsModel>.Instance)
        {
            ProfileVersionId = ProfileVersionId,
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private static INavigationAuthorizationService CreateNavigation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCdcWebAuthorization();

        return services.BuildServiceProvider().GetRequiredService<INavigationAuthorizationService>();
    }

    private sealed class FakeGenerator(params string[] supportedNames) : IProfileReportGenerator
    {
        public Guid? RequestedProfileVersionId { get; private set; }

        public Exception? ThrowOnGenerate { get; init; }

        public bool CanGenerate(string reportName) => supportedNames.Contains(reportName);

        public Task<byte[]> GenerateAsync(string reportName, Guid profileVersionId, CancellationToken cancellationToken)
        {
            RequestedProfileVersionId = profileVersionId;

            return ThrowOnGenerate is not null ? throw ThrowOnGenerate : Task.FromResult(GeneratedPdf);
        }
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
