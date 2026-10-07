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
        IReadOnlyList<ProfileScenarioDto>? whatIfScenarios = null) => new()
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
            WhatIfScenarios = whatIfScenarios ?? []
        };

    private static ProfileHistoryItemDto Version(int number, bool isScenario = false, int minor = 0) => new()
    {
        VersionId = Guid.NewGuid(),
        VersionNumber = number,
        VersionMinor = minor,
        Title = "Title",
        CreatedAtUtc = DateTime.UtcNow,
        IsScenario = isScenario
    };

    private static ProfileScenarioDto Scenario(
        IReadOnlyList<ProfileHistoryItemDto>? published = null,
        IReadOnlyList<ProfileHistoryItemDto>? draft = null) => new()
        {
            ScenarioId = Guid.NewGuid(),
            PublishedVersions = published ?? [],
            DraftVersions = draft ?? []
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
            new AlwaysEnabledLogger<SurveillanceProfilesSearchModel>())
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
        pageModel.HttpContext.Request.Headers.XRequestedWith = "XMLHttpRequest";

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

    [Fact]
    public async Task PerformSearchAsync_FiltersBySelectedLetter_IgnoringLeadingHtmlMarkup()
    {
        var pageModel = CreatePageModel([Profile("<p>Leptospirosis (Weil's Disease)</p>"), Profile("Avian influenza")]);
        pageModel.SelectedLetter = "L";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalResultCount);
        Assert.Equal("<p>Leptospirosis (Weil's Disease)</p>", pageModel.SearchResults[0].Title);
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

    [Theory]
    [InlineData("Az", "<p>Leptospirosis (Weil's Disease)</p>", "<em>Zika virus</em>")]
    [InlineData("Za", "<em>Zika virus</em>", "<p>Leptospirosis (Weil's Disease)</p>")]
    public async Task PerformSearchAsync_SortsByTitle_IgnoringHtmlMarkup(string sortBy, string expectedFirst, string expectedSecond)
    {
        var pageModel = CreatePageModel([Profile("<em>Zika virus</em>"), Profile("<p>Leptospirosis (Weil's Disease)</p>")]);
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
    public void GetVersionGroups_CarriesTheProfileId_ForBrowseProfileLinks()
    {
        var profile = Profile("A", draft: [Version(1)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        Assert.All(groups, group => Assert.Equal(profile.Id, group.ProfileId));
    }

    [Fact]
    public void GetVersionGroups_DraftOnlyProfile_ShowsPlaceholderPublishedCard_ThenRealDraftCard_WhenBothFiltersOn()
    {
        var draftVersion = Version(1);
        var profile = Profile("A", draft: [draftVersion]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        Assert.Equal(2, groups.Count);

        var publishedCard = groups[0];
        Assert.Equal("Published current version", publishedCard.CurrentVersionLabel);
        Assert.Null(publishedCard.CurrentVersion);
        Assert.Equal("(No current published version)", publishedCard.NoVersionMessage);
        Assert.Null(publishedCard.ProfileStatus);

        var draftCard = groups[1];
        Assert.Equal("Draft current version", draftCard.CurrentVersionLabel);
        Assert.Same(draftVersion, draftCard.CurrentVersion);
        Assert.Null(draftCard.NoVersionMessage);
    }

    [Fact]
    public void GetVersionGroups_PlaceholderCard_HasNoProfileStatus_RegardlessOfProfilesOverallStatus()
    {
        var profile = Profile("A", status: "Draft", draft: [Version(1)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        Assert.Null(groups[0].ProfileStatus);
    }

    [Fact]
    public void GetVersionGroups_DraftCard_NeverShowsTheViewReportsLink()
    {
        var profile = Profile("A", published: [Version(2)], draft: [Version(1)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        Assert.True(groups[0].ShowViewReportsLink);
        Assert.False(groups[1].ShowViewReportsLink);
    }

    [Fact]
    public void GetVersionGroups_VersionHistoryRows_ShowViewReportsForPublishedOnly()
    {
        var profile = Profile(
            "A",
            published: [Version(2), Version(1)],
            draft: [Version(4), Version(3)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        Assert.All(groups[0].VersionHistory, row => Assert.True(row.ShowViewReportsLink));
        Assert.All(groups[1].VersionHistory, row => Assert.False(row.ShowViewReportsLink));
    }

    [Fact]
    public void GetVersionGroups_VersionHistory_IncludesTheCurrentVersion_NewestFirst()
    {
        var profile = Profile("A", published: [Version(11), Version(13), Version(12)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: false, includeWhatIfScenarios: false);

        Assert.Equal([13, 12, 11], groups[0].VersionHistory.Select(row => row.Version.VersionNumber));
        Assert.Equal(13, groups[0].CurrentVersion!.VersionNumber);
    }

    [Fact]
    public void GetVersionGroups_VersionHistory_OrdersByMajorThenMinor()
    {
        var profile = Profile(
            "A",
            draft: [Version(12, minor: 1), Version(11, minor: 2), Version(12, minor: 2), Version(11, minor: 10)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, includeWhatIfScenarios: false);

        Assert.Equal(
            [(12, 2), (12, 1), (11, 10), (11, 2)],
            groups[0].VersionHistory.Select(row => (row.Version.VersionNumber, row.Version.VersionMinor)));
        Assert.Equal((12, 2), (groups[0].CurrentVersion!.VersionNumber, groups[0].CurrentVersion!.VersionMinor));
    }

    [Fact]
    public void GetVersionGroups_VersionHistoryRows_CarryTheEffectiveEndDate()
    {
        var endedOn = new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc);
        var superseded = Version(9) with { EffectiveToUtc = endedOn };
        var profile = Profile("A", published: [Version(10), superseded]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: false, includeWhatIfScenarios: false);

        Assert.Null(groups[0].CurrentVersion!.EffectiveToUtc);
        var previous = groups[0].VersionHistory.Single(row => row.Version.VersionNumber == 9);
        Assert.Equal(endedOn, previous.Version.EffectiveToUtc);
    }

    [Fact]
    public void GetVersionGroups_DraftOnlyProfile_ShowsOnlyTheDraftCard_WhenOnlyDraftFilterIsOn()
    {
        var draftVersion = Version(1);
        var profile = Profile("A", draft: [draftVersion]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, includeWhatIfScenarios: false);

        var draftCard = Assert.Single(groups);
        Assert.Same(draftVersion, draftCard.CurrentVersion);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GetVersionGroups_DraftAndPublishedProfile_ShowsOnlyTheRequestedCard(bool displayPublished, bool displayDraft)
    {
        var published = Version(2);
        var draft = Version(1);
        var profile = Profile("A", published: [published], draft: [draft]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished, displayDraft, includeWhatIfScenarios: false);

        var card = Assert.Single(groups);
        Assert.Same(displayPublished ? published : draft, card.CurrentVersion);
    }

    [Fact]
    public void GetVersionGroups_DraftAndPublishedProfile_ShowsPublishedCardFirst_ThenDraftCard()
    {
        var published = Version(2);
        var draft = Version(1);
        var profile = Profile("A", published: [published], draft: [draft]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        Assert.Equal(2, groups.Count);
        Assert.Same(published, groups[0].CurrentVersion);
        Assert.Same(draft, groups[1].CurrentVersion);
    }

    [Fact]
    public void GetVersionGroups_PublishedCard_NeverIncludesTheProfilesOwnDraftVersions_OrWhatIfScenarioVersions()
    {
        var published = Version(2);
        var olderPublished = Version(1);
        var draft = Version(3);
        var scenarioVersion = Version(9, isScenario: true);
        var profile = Profile(
            "A",
            published: [published, olderPublished],
            draft: [draft],
            whatIfScenarios: [Scenario(draft: [scenarioVersion])]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: false);

        var publishedCard = groups[0];
        Assert.Same(published, publishedCard.CurrentVersion);
        Assert.DoesNotContain(publishedCard.VersionHistory, row => row.Version.VersionId == draft.VersionId);
        Assert.DoesNotContain(publishedCard.VersionHistory, row => row.Version.VersionId == scenarioVersion.VersionId);
        Assert.Contains(publishedCard.VersionHistory, row => row.Version.VersionId == olderPublished.VersionId);
    }

    [Fact]
    public void GetVersionGroups_IgnoresWhatIfScenarioCards_AndShowsOnlyPublishedAndDraftCards()
    {
        var currentSituationVersion = Version(1);
        var scenarioAPublished = Version(2, isScenario: true);
        var scenarioBDraft = Version(1, isScenario: true);
        var profile = Profile(
            "A",
            published: [currentSituationVersion],
            whatIfScenarios: [Scenario(published: [scenarioAPublished]), Scenario(draft: [scenarioBDraft])]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, includeWhatIfScenarios: true);

        Assert.Equal(2, groups.Count);
        Assert.Same(currentSituationVersion, groups[0].CurrentVersion);
        Assert.Same(groups[1].CurrentVersion, groups[1].CurrentVersion);
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

    // The legacy page used a radio group, so exactly one mode was always active and
    // "this exact word or phrase" won; these cover every combination the querystring can carry.
    [Theory]
    [InlineData(true, false, SearchForType.ExactWordOrPhrase)]
    [InlineData(false, true, SearchForType.AllWords)]
    [InlineData(true, true, SearchForType.ExactWordOrPhrase)]
    [InlineData(false, false, SearchForType.ExactWordOrPhrase)]
    public void SearchForType_ResolvesTheTwoCheckboxesToASingleMode(
        bool searchForExactPhrase,
        bool searchForAllWords,
        SearchForType expected)
    {
        var pageModel = CreatePageModel([]);
        pageModel.SearchForExactPhrase = searchForExactPhrase;
        pageModel.SearchForAllWords = searchForAllWords;

        Assert.Equal(expected, pageModel.SearchForType);
    }

    private static FakeUrlHelper CreateUrlHelper() => new(values =>
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


