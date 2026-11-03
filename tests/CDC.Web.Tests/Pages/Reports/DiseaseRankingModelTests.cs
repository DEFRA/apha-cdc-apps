using System.Security.Claims;
using CDC.Common.Contracts;
using CDC.Web.Authorization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;
using CDC.Web.Features.DiseaseRanking;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.Reports;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.Reports;

public class DiseaseRankingModelTests
{
    private static readonly Guid StoredVersionId = Guid.NewGuid();

    [Fact]
    public async Task OnGetAsync_ListsTheReports_ForAProfileEditor()
    {
        var apiClient = new FakeApiClient();
        var pageModel = CreatePageModel(apiClient);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.CanGenerateReports);
        Assert.Single(pageModel.Options);
        Assert.Null(apiClient.LastStaticReportsRequest);
    }

    [Theory]
    [InlineData(AuthorizationRoles.PolicyProfileUser)]
    [InlineData(null)]
    public async Task OnGetAsync_SendsEveryoneElseToTheStoredReport(string? role)
    {
        var apiClient = new FakeApiClient(staticReports: [Report("Some other report", Guid.NewGuid()), Report("DiseaseRankingReport", StoredVersionId)]);
        var pageModel = CreatePageModel(apiClient, role: role);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/static-reports/versions/{StoredVersionId}/document?download=true", redirect.Url);
        Assert.False(pageModel.CanGenerateReports);
        Assert.Empty(pageModel.Options);
        Assert.Equal((false, false), apiClient.LastStaticReportsRequest);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenNoStoredReportHasBeenUploaded()
    {
        var pageModel = CreatePageModel(new FakeApiClient(staticReports: [Report("Some other report", Guid.NewGuid())]), role: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnErrorInsteadOfTheReports_WhenTheStoredReportCannotBeLoaded()
    {
        var pageModel = CreatePageModel(new FakeApiClient(throwOnGetCurrentStaticReports: new HttpRequestException("down")), role: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.CanGenerateReports);
        Assert.Equal("The report could not be loaded. Try again later", Assert.Single(pageModel.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Posts_AreForbidden_ForAUserWhoIsNotAProfileEditor()
    {
        var pdf = new StubPdfService();
        var pageModel = CreatePageModel(new FakeApiClient(), pdf, AuthorizationRoles.PolicyProfileUser);
        pageModel.ScoredReportKey = "terrestrial";
        pageModel.RankedReportKey = "terrestrial";

        Assert.IsType<ForbidResult>(await pageModel.OnPostScoredAsync(CancellationToken.None));
        Assert.IsType<ForbidResult>(await pageModel.OnPostRankedAsync(CancellationToken.None));
        Assert.Equal(0, pdf.Calls);
        Assert.False(pageModel.CanGenerateReports);
    }

    [Fact]
    public async Task OnPostScoredAsync_DownloadsThePdf_ForAProfileEditor()
    {
        var pdf = new StubPdfService();
        var pageModel = CreatePageModel(new FakeApiClient(), pdf);
        pageModel.ScoredReportKey = "terrestrial";

        var result = await pageModel.OnPostScoredAsync(CancellationToken.None);

        Assert.Equal("report.pdf", Assert.IsType<FileContentResult>(result).FileDownloadName);
        Assert.True(pageModel.CanGenerateReports);
        Assert.Equal(1, pdf.Calls);
    }

    private static DiseaseRankingModel CreatePageModel(
        FakeApiClient apiClient,
        StubPdfService? pdf = null,
        string? role = AuthorizationRoles.ProfileEditor)
    {
        var claims = new List<Claim>();

        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };

        return new DiseaseRankingModel(
            apiClient,
            new StubRankingApi(),
            pdf ?? new StubPdfService(),
            CreateNavigation(),
            NullLogger<DiseaseRankingModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
    }

    private static INavigationAuthorizationService CreateNavigation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCdcWebAuthorization();

        return services.BuildServiceProvider().GetRequiredService<INavigationAuthorizationService>();
    }

    private static StaticReportListItemDto Report(string title, Guid versionId) => new()
    {
        Id = versionId,
        StaticReportId = Guid.NewGuid(),
        Title = title,
        VersionMajor = 1,
        EffectiveDateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EffectiveDateTo = null,
        IsUserManual = false,
        IsPublic = false,
        FileSize = 1024
    };

    private sealed class StubRankingApi : IDiseaseRankingApiService
    {
        public Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DiseaseRankingReportOptionDto>>([new DiseaseRankingReportOptionDto { Key = "terrestrial", Label = "Terrestrial" }]);

        public Task<DiseaseRankingReportDto?> GetReportAsync(string reportKey, DiseaseRankingReportKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<DiseaseRankingReportDto?>(null);
    }

    private sealed class StubPdfService : IDiseaseRankingPdfService
    {
        public int Calls { get; private set; }

        public Task<GeneratedPdf?> GenerateAsync(string reportKey, DiseaseRankingReportKind kind, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult<GeneratedPdf?>(new GeneratedPdf("report.pdf", [1, 2, 3]));
        }
    }
}
