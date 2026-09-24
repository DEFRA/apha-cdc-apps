using CDC.Web.Models;
using CDC.Web.Pages;
using CDC.Web.Tests.Features.Landing;
using CDC.Web.Tests.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class SurveillanceProfilesSearchModelTests
{
    private static ProfileSearchResultDto Profile(
        string title,
        string status = "Published",
        DateTime? modifiedAtUtc = null,
        IReadOnlyList<ProfileHistoryItemDto>? published = null,
        IReadOnlyList<ProfileHistoryItemDto>? draft = null,
        IReadOnlyList<ProfileHistoryItemDto>? scenarios = null) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = modifiedAtUtc ?? DateTime.UtcNow,
            IsPublic = true,
            AffectedSpecies = [],
            PublishedVersions = published ?? [],
            DraftVersions = draft ?? [],
            Scenarios = scenarios ?? []
        };

    private static ProfileHistoryItemDto Version(int number, bool isScenario = false) => new()
    {
        VersionId = Guid.NewGuid(),
        VersionNumber = number,
        Title = "Title",
        CreatedAtUtc = DateTime.UtcNow,
        IsScenario = isScenario
    };

    private static SurveillanceProfilesSearchModel CreatePageModel(
        IReadOnlyList<ProfileSearchResultDto>? searchResults = null,
        Exception? throwOnSearchProfiles = null,
        IReadOnlyList<SpeciesDto>? species = null,
        Exception? throwOnGetAllSpecies = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new SurveillanceProfilesSearchModel(
            new FakeApiClient(searchResults: searchResults, throwOnSearchProfiles: throwOnSearchProfiles),
            new FakeSpeciesApiService(species, throwOnGetAllSpecies),
            NullLogger<SurveillanceProfilesSearchModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };

        return pageModel;
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPage_ByDefault()
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis")]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.IsSearchPerformed);
        Assert.Equal(1, pageModel.TotalResultCount);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsResultsPartial_WhenRequestedByAjax()
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis")]);
        pageModel.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_SearchResults", partial.ViewName);
        Assert.Same(pageModel, partial.Model);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsPage()
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis")]);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.IsSearchPerformed);
    }

    [Fact]
    public async Task PerformSearchAsync_FiltersBySelectedLetter()
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis"), Profile("Avian influenza")]);
        pageModel.SelectedLetter = "B";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalResultCount);
        Assert.Equal("Bovine tuberculosis", pageModel.SearchResults[0].Title);
    }

    [Theory]
    [InlineData("Az", "Avian influenza", "Bovine tuberculosis")]
    [InlineData("Za", "Bovine tuberculosis", "Avian influenza")]
    [InlineData("Unknown", "Avian influenza", "Bovine tuberculosis")]
    public async Task PerformSearchAsync_SortsByTitle(string sortBy, string expectedFirst, string expectedSecond)
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis"), Profile("Avian influenza")]);
        pageModel.SortBy = sortBy;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedFirst, pageModel.SearchResults[0].Title);
        Assert.Equal(expectedSecond, pageModel.SearchResults[1].Title);
    }

    [Fact]
    public async Task PerformSearchAsync_SortsByMostRecentlyUpdated()
    {
        var older = Profile("Older", modifiedAtUtc: DateTime.UtcNow.AddDays(-5));
        var newer = Profile("Newer", modifiedAtUtc: DateTime.UtcNow);
        var pageModel = CreatePageModel([older, newer]);
        pageModel.SortBy = "MostRecentlyUpdated";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Newer", pageModel.SearchResults[0].Title);
    }

    [Fact]
    public async Task PerformSearchAsync_SortsByLeastRecentlyUpdated()
    {
        var older = Profile("Older", modifiedAtUtc: DateTime.UtcNow.AddDays(-5));
        var newer = Profile("Newer", modifiedAtUtc: DateTime.UtcNow);
        var pageModel = CreatePageModel([older, newer]);
        pageModel.SortBy = "LeastRecentlyUpdated";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Older", pageModel.SearchResults[0].Title);
    }

    [Theory]
    [InlineData("10", 1)]
    [InlineData("not-a-number", 1)]
    [InlineData("All", 1)]
    public async Task PerformSearchAsync_PagesResults(string pageSize, int expectedTotalPages)
    {
        var pageModel = CreatePageModel([Profile("A"), Profile("B"), Profile("C")]);
        pageModel.PageSize = pageSize;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedTotalPages, pageModel.TotalPages);
        Assert.Equal(3, pageModel.PagedResults.Count);
    }

    [Fact]
    public async Task PerformSearchAsync_SplitsResultsAcrossPages_AndClampsPageNumber()
    {
        var pageModel = CreatePageModel([Profile("A"), Profile("B"), Profile("C")]);
        pageModel.PageSize = "2";
        pageModel.PageNumber = 99;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.TotalPages);
        Assert.Equal(2, pageModel.PageNumber);
        Assert.Single(pageModel.PagedResults);
    }

    [Fact]
    public async Task PerformSearchAsync_ReturnsNoResults_WhenAllDisplayFlagsAreFalse()
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis")]);
        pageModel.DisplayPublished = false;
        pageModel.DisplayDraft = false;
        pageModel.DisplayScenarios = false;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(0, pageModel.TotalResultCount);
        Assert.Equal(1, pageModel.TotalPages);
        Assert.Empty(pageModel.PagedResults);
    }

    [Fact]
    public async Task PerformSearchAsync_SetsFriendlyError_WhenApiThrowsHttpRequestException()
    {
        var pageModel = CreatePageModel(throwOnSearchProfiles: new HttpRequestException("connection refused"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(pageModel.ErrorMessage));
        Assert.Empty(pageModel.SearchResults);
        Assert.Empty(pageModel.PagedResults);
    }

    [Fact]
    public async Task PerformSearchAsync_SetsFriendlyError_WhenApiThrowsUnexpectedException()
    {
        var pageModel = CreatePageModel(throwOnSearchProfiles: new InvalidOperationException("boom"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(pageModel.ErrorMessage));
        Assert.Empty(pageModel.SearchResults);
    }

    [Fact]
    public async Task LoadSpeciesTreeAsync_BuildsTree_AndJoinsSelectedSpeciesLabels()
    {
        var cattleId = Guid.NewGuid();
        IReadOnlyList<SpeciesDto> species = [new SpeciesDto { Id = cattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true }];
        var pageModel = CreatePageModel([], species: species);
        pageModel.SelectedSpecies = [cattleId.ToString()];

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Single(pageModel.SpeciesTree.Nodes);
        Assert.Equal("Cattle", pageModel.SelectedSpeciesLabel);
    }

    [Fact]
    public async Task LoadSpeciesTreeAsync_DefaultsLabel_WhenNoSpeciesSelected()
    {
        var pageModel = CreatePageModel([]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Any species", pageModel.SelectedSpeciesLabel);
    }

    [Fact]
    public async Task LoadSpeciesTreeAsync_FallsBackToEmptyTree_WhenApiFails()
    {
        var pageModel = CreatePageModel([], throwOnGetAllSpecies: new HttpRequestException("down"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.SpeciesTree.Nodes);
        Assert.Equal("Any species", pageModel.SelectedSpeciesLabel);
    }

    [Fact]
    public void GetCurrentVersion_PrefersPublished_ThenDraft_ThenScenario()
    {
        var published = Version(2);
        var draft = Version(3);
        var scenario = Version(1, isScenario: true);

        var profileWithPublished = Profile("A", published: [published], draft: [draft], scenarios: [scenario]);
        Assert.Same(published, SurveillanceProfilesSearchModel.GetCurrentVersion(profileWithPublished));

        var profileWithDraftOnly = Profile("B", draft: [draft], scenarios: [scenario]);
        Assert.Same(draft, SurveillanceProfilesSearchModel.GetCurrentVersion(profileWithDraftOnly));

        var profileWithScenarioOnly = Profile("C", scenarios: [scenario]);
        Assert.Same(scenario, SurveillanceProfilesSearchModel.GetCurrentVersion(profileWithScenarioOnly));

        var profileWithNoVersions = Profile("D");
        Assert.Null(SurveillanceProfilesSearchModel.GetCurrentVersion(profileWithNoVersions));
    }

    [Theory]
    [InlineData(true, false, false, "Published current version")]
    [InlineData(false, true, false, "Draft current version")]
    [InlineData(false, false, true, "Scenario version")]
    [InlineData(false, false, false, "Version")]
    public void GetCurrentVersionLabel_MatchesTheBucketWithVersions(bool hasPublished, bool hasDraft, bool hasScenario, string expectedLabel)
    {
        var profile = Profile(
            "A",
            published: hasPublished ? [Version(1)] : [],
            draft: hasDraft ? [Version(1)] : [],
            scenarios: hasScenario ? [Version(1, isScenario: true)] : []);

        Assert.Equal(expectedLabel, SurveillanceProfilesSearchModel.GetCurrentVersionLabel(profile));
    }

    [Fact]
    public void GetPreviousVersions_ExcludesCurrentVersion_AndOrdersByVersionNumberDescending()
    {
        var current = Version(3);
        var older = Version(2);
        var oldest = Version(1);
        var profile = Profile("A", published: [current, older], draft: [oldest]);

        var previous = SurveillanceProfilesSearchModel.GetPreviousVersions(profile);

        Assert.Equal(2, previous.Count);
        Assert.Equal(older.VersionId, previous[0].Version.VersionId);
        Assert.Equal("Published", previous[0].Status);
        Assert.Equal(oldest.VersionId, previous[1].Version.VersionId);
        Assert.Equal("Draft", previous[1].Status);
    }

    [Fact]
    public void BuildLetterUrl_IncludesFiltersAndEverySelectedSpecies()
    {
        var pageModel = CreatePageModel([]);
        pageModel.SearchText = "tb";
        pageModel.SelectedSpecies = ["cattle", "sheep"];
        pageModel.Url = CreateUrlHelper();

        var url = pageModel.BuildLetterUrl("B");

        Assert.NotNull(url);
        Assert.Contains("SelectedLetter=B", url);
        Assert.Contains("SearchText=tb", url);
        Assert.Contains("SelectedSpecies=cattle", url);
        Assert.Contains("SelectedSpecies=sheep", url);
    }

    [Fact]
    public void BuildPageUrl_IncludesRequestedPageNumber()
    {
        var pageModel = CreatePageModel([]);
        pageModel.Url = CreateUrlHelper();

        var url = pageModel.BuildPageUrl(3);

        Assert.NotNull(url);
        Assert.Contains("PageNumber=3", url);
    }

    [Fact]
    public void BuildLetterUrl_ReturnsNull_WhenTheUrlHelperCannotResolveThePage()
    {
        var pageModel = CreatePageModel([]);
        pageModel.Url = new FakeUrlHelper(_ => null);

        var url = pageModel.BuildLetterUrl("All");

        Assert.Null(url);
    }

    private static IUrlHelper CreateUrlHelper() => new FakeUrlHelper(values =>
    {
        var query = string.Join("&", values.Select(pair => $"{pair.Key}={pair.Value}"));

        return $"/SurveillanceProfiles/Search?{query}";
    });

    /// <summary>Minimal <see cref="IUrlHelper"/> double: only <see cref="RouteUrl"/> is used by
    /// <see cref="SurveillanceProfilesSearchModel"/>.</summary>
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


