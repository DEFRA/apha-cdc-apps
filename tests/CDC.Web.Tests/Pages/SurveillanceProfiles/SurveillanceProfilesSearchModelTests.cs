using CDC.Web.Models;
using CDC.Web.Pages;
using CDC.Web.Tests.Features.Landing;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.TestSupport;
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
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0);

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ProfileSearchResultDto Profile(
        string title,
        string status = "Published",
        DateTime? modifiedAtUtc = null,
        IReadOnlyList<ProfileHistoryItemDto>? published = null,
        IReadOnlyList<ProfileHistoryItemDto>? draft = null,
        IReadOnlyList<ProfileScenarioDto>? whatIfScenarios = null,
        IReadOnlyList<string>? affectedSpecies = null) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = modifiedAtUtc ?? DateTime.UtcNow,
            IsPublic = true,
            AffectedSpecies = affectedSpecies ?? [],
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
        IReadOnlyList<ProfileHistoryItemDto>? draft = null,
        string title = "") => new()
        {
            ScenarioId = Guid.NewGuid(),
            ScenarioTitle = title,
            PublishedVersions = published ?? [],
            DraftVersions = draft ?? []
        };

    private static SurveillanceProfilesSearchModel CreatePageModel(
        IReadOnlyList<ProfileSearchResultDto>? searchResults = null,
        Exception? throwOnSearchProfiles = null,
        IReadOnlyList<SpeciesDto>? species = null,
        Exception? throwOnGetAllSpecies = null,
        TimeProvider? timeProvider = null,
        ISession? session = null,
        string? query = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext { Session = session ?? new FakeSession() };

        if (query is not null)
        {
            httpContext.Request.QueryString = new QueryString(query);
        }

        var pageModel = new SurveillanceProfilesSearchModel(
            new FakeApiClient(searchResults: searchResults, throwOnSearchProfiles: throwOnSearchProfiles),
            new FakeSpeciesApiService(species, throwOnGetAllSpecies),
            new AlwaysEnabledLogger<SurveillanceProfilesSearchModel>(),
            timeProvider)
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext,
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
    public async Task OnGetAsync_KeepsTheDefaultFilters_AndRemembersNothing_WhenNoFilterHasBeenChosen()
    {
        var session = new FakeSession();
        var pageModel = CreatePageModel(session: session);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.DisplayPublished);
        Assert.False(pageModel.DisplayDraft);
        Assert.False(pageModel.DisplayScenarios);
        Assert.Equal("All", pageModel.SelectedLetter);
        Assert.Empty(session.Keys);
    }

    // The legacy page kept the Display checkboxes and the letter in the session, so coming back from Browse profile found them as left.
    [Fact]
    public async Task OnGetAsync_BringsBackTheFiltersLastChosen_WhenTheAddressCarriesNone()
    {
        var session = new FakeSession();
        var chosen = CreatePageModel(session: session, query: "?DisplayPublished=false&DisplayDraft=true&DisplayScenarios=true&SelectedLetter=B");
        chosen.DisplayPublished = false;
        chosen.DisplayDraft = true;
        chosen.DisplayScenarios = true;
        chosen.SelectedLetter = "B";
        await chosen.OnGetAsync(CancellationToken.None);

        var returning = CreatePageModel(session: session);
        await returning.OnGetAsync(CancellationToken.None);

        Assert.False(returning.DisplayPublished);
        Assert.True(returning.DisplayDraft);
        Assert.True(returning.DisplayScenarios);
        Assert.Equal("B", returning.SelectedLetter);
    }

    [Fact]
    public async Task OnGetAsync_BringsBackTheFiltersForTheResultsToo()
    {
        var session = new FakeSession();
        var chosen = CreatePageModel(session: session, query: "?SelectedLetter=B");
        chosen.SelectedLetter = "B";
        await chosen.OnGetAsync(CancellationToken.None);

        var returning = CreatePageModel([Profile("Bovine tuberculosis"), Profile("Avian influenza")], session: session);
        await returning.OnGetAsync(CancellationToken.None);

        Assert.Equal("Bovine tuberculosis", Assert.Single(returning.SearchResults).Title);
    }

    [Fact]
    public async Task OnGetAsync_PrefersTheFiltersInTheAddress_AndRemembersThemInPlaceOfTheEarlierOnes()
    {
        var session = new FakeSession();
        var earlier = CreatePageModel(session: session, query: "?DisplayPublished=true&DisplayDraft=true&DisplayScenarios=true&SelectedLetter=B");
        earlier.DisplayDraft = true;
        earlier.DisplayScenarios = true;
        earlier.SelectedLetter = "B";
        await earlier.OnGetAsync(CancellationToken.None);

        var later = CreatePageModel(session: session, query: "?DisplayPublished=true&DisplayDraft=false&DisplayScenarios=false&SelectedLetter=All");
        await later.OnGetAsync(CancellationToken.None);
        var returning = CreatePageModel(session: session);
        await returning.OnGetAsync(CancellationToken.None);

        Assert.False(later.DisplayDraft);
        Assert.False(later.DisplayScenarios);
        Assert.Equal("All", later.SelectedLetter);
        Assert.True(returning.DisplayPublished);
        Assert.False(returning.DisplayDraft);
        Assert.False(returning.DisplayScenarios);
        Assert.Equal("All", returning.SelectedLetter);
    }

    [Fact]
    public async Task OnGetAsync_FillsInOnlyTheFiltersTheAddressLeavesOut()
    {
        var session = new FakeSession();
        var earlier = CreatePageModel(session: session, query: "?DisplayPublished=false&DisplayDraft=true&DisplayScenarios=true&SelectedLetter=C");
        earlier.DisplayPublished = false;
        earlier.DisplayDraft = true;
        earlier.DisplayScenarios = true;
        earlier.SelectedLetter = "C";
        await earlier.OnGetAsync(CancellationToken.None);

        var later = CreatePageModel(session: session, query: "?DisplayScenarios=false&SearchText=bovine");
        later.SearchText = "bovine";
        await later.OnGetAsync(CancellationToken.None);
        var returning = CreatePageModel(session: session);
        await returning.OnGetAsync(CancellationToken.None);

        Assert.False(later.DisplayScenarios);
        Assert.False(later.DisplayPublished);
        Assert.True(later.DisplayDraft);
        Assert.Equal("C", later.SelectedLetter);
        Assert.False(returning.DisplayScenarios);
        Assert.False(returning.DisplayPublished);
        Assert.True(returning.DisplayDraft);
        Assert.Equal("C", returning.SelectedLetter);
    }

    // As in legacy, only the Display checkboxes and the letter are kept; the keyword, sort order and page size start afresh.
    [Fact]
    public async Task OnGetAsync_DoesNotRemember_TheKeywordTheSortOrderOrThePageSize()
    {
        var session = new FakeSession();
        var searched = CreatePageModel(session: session, query: "?SearchText=bovine&SortBy=Za&PageSize=30");
        searched.SearchText = "bovine";
        await searched.OnGetAsync(CancellationToken.None);

        var returning = CreatePageModel(session: session);
        await returning.OnGetAsync(CancellationToken.None);

        Assert.Empty(session.Keys);
        Assert.Null(returning.SearchText);
        Assert.Equal("Az", returning.SortBy);
        Assert.Equal("10", returning.PageSize);
    }

    // The alphabet links offer only "All" and A to Z; anything else typed into the address is not something to come back to.
    [Theory]
    [InlineData("B", "B")]
    [InlineData("b", "B")]
    [InlineData("All", "All")]
    [InlineData("Bov", "All")]
    [InlineData("1", "All")]
    [InlineData("", "All")]
    public async Task OnGetAsync_RemembersOnlyALetterTheAlphabetLinksOffer(string chosen, string expectedOnReturn)
    {
        var session = new FakeSession();
        var earlier = CreatePageModel(session: session, query: $"?SelectedLetter={chosen}");
        earlier.SelectedLetter = chosen;
        await earlier.OnGetAsync(CancellationToken.None);

        var returning = CreatePageModel(session: session);
        await returning.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedOnReturn, returning.SelectedLetter);
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

    [Fact]
    public async Task PerformSearchAsync_FiltersBySelectedSpecies()
    {
        var cattleId = Guid.NewGuid().ToString();
        var sheepId = Guid.NewGuid().ToString();
        var pageModel = CreatePageModel(
        [
            Profile("Bovine tuberculosis", affectedSpecies: [cattleId]),
            Profile("Sheep scab", affectedSpecies: [sheepId])
        ]);
        pageModel.SelectedSpecies = [cattleId];

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalResultCount);
        Assert.Equal("Bovine tuberculosis", pageModel.SearchResults[0].Title);
    }

    [Fact]
    public async Task PerformSearchAsync_MatchesAProfile_WhenAnyOfSeveralSelectedSpeciesMatch()
    {
        var cattleId = Guid.NewGuid().ToString();
        var sheepId = Guid.NewGuid().ToString();
        var pigId = Guid.NewGuid().ToString();
        var pageModel = CreatePageModel(
        [
            Profile("Bovine tuberculosis", affectedSpecies: [cattleId]),
            Profile("Swine fever", affectedSpecies: [pigId])
        ]);
        pageModel.SelectedSpecies = [sheepId, pigId];

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalResultCount);
        Assert.Equal("Swine fever", pageModel.SearchResults[0].Title);
    }

    [Fact]
    public async Task PerformSearchAsync_DoesNotFilterBySpecies_WhenNoneAreSelected()
    {
        var pageModel = CreatePageModel(
        [
            Profile("Bovine tuberculosis", affectedSpecies: [Guid.NewGuid().ToString()]),
            Profile("Avian influenza", affectedSpecies: [])
        ]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.TotalResultCount);
    }

    [Fact]
    public async Task PerformSearchAsync_SelectingAParentSpecies_AlsoMatchesItsChildSpecies()
    {
        var cattleId = Guid.NewGuid();
        var dairyId = Guid.NewGuid();
        var sheepId = Guid.NewGuid();
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = cattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = dairyId, ParentId = cattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = sheepId, ParentId = Guid.Empty, Description = "Sheep", IsActive = true, IsInUse = true }
        ];
        var pageModel = CreatePageModel(
        [
            Profile("Bovine tuberculosis", affectedSpecies: [dairyId.ToString()]),
            Profile("Sheep scab", affectedSpecies: [sheepId.ToString()])
        ], species: species);
        pageModel.SelectedSpecies = [cattleId.ToString()];

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalResultCount);
        Assert.Equal("Bovine tuberculosis", pageModel.SearchResults[0].Title);
    }

    [Fact]
    public async Task PerformSearchAsync_SelectingAGrandparentSpecies_MatchesGrandchildSpecies()
    {
        var cattleId = Guid.NewGuid();
        var dairyId = Guid.NewGuid();
        var jerseyId = Guid.NewGuid();
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = cattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = dairyId, ParentId = cattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = jerseyId, ParentId = dairyId, Description = "Jersey", IsActive = true, IsInUse = true }
        ];
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis", affectedSpecies: [jerseyId.ToString()])], species: species);
        pageModel.SelectedSpecies = [cattleId.ToString()];

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.TotalResultCount);
    }

    [Fact]
    public async Task PerformSearchAsync_SelectingAChildSpecies_DoesNotMatchItsParentOrSiblingSpecies()
    {
        var cattleId = Guid.NewGuid();
        var dairyId = Guid.NewGuid();
        var beefId = Guid.NewGuid();
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = cattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = dairyId, ParentId = cattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = beefId, ParentId = cattleId, Description = "Beef cattle", IsActive = true, IsInUse = true }
        ];
        var pageModel = CreatePageModel(
        [
            Profile("Bovine tuberculosis (all cattle)", affectedSpecies: [cattleId.ToString()]),
            Profile("Beef-only disease", affectedSpecies: [beefId.ToString()])
        ], species: species);
        pageModel.SelectedSpecies = [dairyId.ToString()];

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(0, pageModel.TotalResultCount);
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
    public async Task PerformSearchAsync_ReturnsNoResults_WhenOnlyDisplayScenariosIsOn()
    {
        var pageModel = CreatePageModel([Profile("Bovine tuberculosis", whatIfScenarios: [Scenario(draft: [Version(1, isScenario: true)])])]);
        pageModel.DisplayPublished = false;
        pageModel.DisplayDraft = false;
        pageModel.DisplayScenarios = true;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(0, pageModel.TotalResultCount);
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

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

        Assert.All(groups, group => Assert.Equal(profile.Id, group.ProfileId));
    }

    [Fact]
    public void GetVersionGroups_DraftOnlyProfile_ShowsPlaceholderPublishedCard_ThenRealDraftCard_WhenBothFiltersOn()
    {
        var draftVersion = Version(1);
        var profile = Profile("A", draft: [draftVersion]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

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

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

        Assert.Null(groups[0].ProfileStatus);
    }

    [Fact]
    public void GetVersionGroups_DraftCard_NeverShowsTheViewReportsLink()
    {
        var profile = Profile("A", published: [Version(2)], draft: [Version(1)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

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

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

        Assert.All(groups[0].VersionHistory, row => Assert.True(row.ShowViewReportsLink));
        Assert.All(groups[1].VersionHistory, row => Assert.False(row.ShowViewReportsLink));
    }

    [Fact]
    public void GetVersionGroups_VersionHistory_IncludesTheCurrentVersion_NewestFirst()
    {
        var profile = Profile("A", published: [Version(11), Version(13), Version(12)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: false, now: Now);

        Assert.Equal([13, 12, 11], groups[0].VersionHistory.Select(row => row.Version.VersionNumber));
        Assert.Equal(13, groups[0].CurrentVersion!.VersionNumber);
    }

    [Fact]
    public void GetVersionGroups_VersionHistory_OrdersByMajorThenMinor()
    {
        var profile = Profile(
            "A",
            draft: [Version(12, minor: 1), Version(11, minor: 2), Version(12, minor: 2), Version(11, minor: 10)]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, now: Now);

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

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: false, now: Now);

        Assert.Null(groups[0].CurrentVersion!.EffectiveToUtc);
        var previous = groups[0].VersionHistory.Single(row => row.Version.VersionNumber == 9);
        Assert.Equal(endedOn, previous.Version.EffectiveToUtc);
    }

    [Fact]
    public void GetVersionGroups_DraftOnlyProfile_ShowsOnlyTheDraftCard_WhenOnlyDraftFilterIsOn()
    {
        var draftVersion = Version(1);
        var profile = Profile("A", draft: [draftVersion]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, now: Now);

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

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished, displayDraft, Now);

        var card = Assert.Single(groups);
        Assert.Same(displayPublished ? published : draft, card.CurrentVersion);
    }

    [Fact]
    public void GetVersionGroups_DraftAndPublishedProfile_ShowsPublishedCardFirst_ThenDraftCard()
    {
        var published = Version(2);
        var draft = Version(1);
        var profile = Profile("A", published: [published], draft: [draft]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

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

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

        var publishedCard = groups[0];
        Assert.Same(published, publishedCard.CurrentVersion);
        Assert.DoesNotContain(publishedCard.VersionHistory, row => row.Version.VersionId == draft.VersionId);
        Assert.DoesNotContain(publishedCard.VersionHistory, row => row.Version.VersionId == scenarioVersion.VersionId);
        Assert.Contains(publishedCard.VersionHistory, row => row.Version.VersionId == olderPublished.VersionId);
    }

    [Fact]
    public void GetVersionGroups_ShowsOnlyTheOwnLineagesPublishedAndDraftCards_NeverTheWhatIfScenarios()
    {
        var currentSituationVersion = Version(1);
        var scenarioAPublished = Version(2, isScenario: true);
        var scenarioBDraft = Version(1, isScenario: true);
        var profile = Profile(
            "A",
            published: [currentSituationVersion],
            whatIfScenarios: [Scenario(published: [scenarioAPublished]), Scenario(draft: [scenarioBDraft])]);

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: true, displayDraft: true, now: Now);

        Assert.Equal(2, groups.Count);
        Assert.Same(currentSituationVersion, groups[0].CurrentVersion);
        Assert.Null(groups[1].CurrentVersion);
    }

    [Fact]
    public void GetScenarioSections_ListsOnlyTheOwnLineage_WhenScenariosAreNotDisplayed()
    {
        var profile = Profile(
            "A",
            published: [Version(1)],
            whatIfScenarios: [Scenario(draft: [Version(1, isScenario: true)], title: "Scenario A")]) with
        {
            ScenarioTitle = "Current situation"
        };

        var sections = SurveillanceProfilesSearchModel.GetScenarioSections(
            profile, displayPublished: true, displayDraft: true, displayScenarios: false, now: Now);

        var section = Assert.Single(sections);
        Assert.Equal(profile.Id, section.ScenarioId);
        Assert.Equal("Current situation", section.Title);
        Assert.Equal(2, section.Groups.Count);
    }

    [Fact]
    public void GetScenarioSections_ListsTheOwnLineageFirst_ThenEachScenarioWithItsOwnCards_WhenScenariosAreDisplayed()
    {
        var ownPublished = Version(1);
        var scenarioAPublished = Version(2, isScenario: true);
        var scenarioADraft = Version(3, isScenario: true);
        var scenarioBDraft = Version(1, isScenario: true);
        var scenarioA = Scenario(published: [scenarioAPublished], draft: [scenarioADraft], title: "Scenario A");
        var scenarioB = Scenario(draft: [scenarioBDraft], title: "Scenario B");
        var profile = Profile("A", published: [ownPublished], whatIfScenarios: [scenarioA, scenarioB]) with
        {
            ScenarioTitle = "Current situation"
        };

        var sections = SurveillanceProfilesSearchModel.GetScenarioSections(
            profile, displayPublished: true, displayDraft: true, displayScenarios: true, now: Now);

        Assert.Equal(["Current situation", "Scenario A", "Scenario B"], sections.Select(section => section.Title));
        Assert.Equal([profile.Id, scenarioA.ScenarioId, scenarioB.ScenarioId], sections.Select(section => section.ScenarioId));

        Assert.Same(ownPublished, sections[0].Groups[0].CurrentVersion);
        Assert.Same(scenarioAPublished, sections[1].Groups[0].CurrentVersion);
        Assert.Same(scenarioADraft, sections[1].Groups[1].CurrentVersion);
        Assert.Null(sections[2].Groups[0].CurrentVersion);
        Assert.Same(scenarioBDraft, sections[2].Groups[1].CurrentVersion);

        Assert.All(sections, section => Assert.All(section.Groups, group => Assert.Equal(section.ScenarioId, group.ProfileId)));

        var sectionIds = sections.SelectMany(section => section.Groups).Select(group => group.SectionId).ToList();
        Assert.Equal(sectionIds.Count, sectionIds.Distinct().Count());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GetScenarioSections_GivesEveryScenarioOnlyTheCardsTheDisplayFiltersAllow(bool displayPublished, bool displayDraft)
    {
        var profile = Profile(
            "A",
            published: [Version(1)],
            draft: [Version(2)],
            whatIfScenarios: [Scenario(published: [Version(1, isScenario: true)], draft: [Version(2, isScenario: true)], title: "Scenario A")]);

        var sections = SurveillanceProfilesSearchModel.GetScenarioSections(
            profile, displayPublished, displayDraft, displayScenarios: true, now: Now);

        Assert.Equal(2, sections.Count);
        Assert.All(sections, section => Assert.Single(section.Groups));
    }

    [Fact]
    public void GetScenarioSections_GivesEveryScenarioItsOwnProfileStatusAndReview()
    {
        var scenario = Scenario(published: [Version(1, isScenario: true)], draft: [Version(2, isScenario: true)], title: "Scenario A") with
        {
            ProfileStatusName = "Under review",
            NextReviewDate = Now.AddDays(30)
        };
        var profile = Profile("A", published: [Version(1)], draft: [Version(2)], whatIfScenarios: [scenario]) with
        {
            ProfileStatusName = "Active",
            ReviewIncomplete = true
        };

        var sections = SurveillanceProfilesSearchModel.GetScenarioSections(
            profile, displayPublished: true, displayDraft: true, displayScenarios: true, now: Now);

        Assert.Equal(["Published", "Active"], sections[0].Groups.Select(group => group.ProfileStatus));
        Assert.Equal([null, "Review incomplete"], sections[0].Groups.Select(group => group.ReviewStatus));
        Assert.Equal(["Published", "Under review"], sections[1].Groups.Select(group => group.ProfileStatus));
        Assert.Equal([null, "Next review: 15/07/2026"], sections[1].Groups.Select(group => group.ReviewStatus));
    }

    [Fact]
    public void GetScenarioSections_OnThePageModel_UsesItsFiltersAndTheCurrentTime()
    {
        var pageModel = CreatePageModel([], timeProvider: new FixedTimeProvider(Now));
        pageModel.DisplayPublished = false;
        pageModel.DisplayDraft = true;
        pageModel.DisplayScenarios = false;
        var profile = Profile("A", draft: [Version(1)]) with { NextReviewDate = Now.AddDays(-1) };

        var section = Assert.Single(pageModel.GetScenarioSections(profile));

        var draftCard = Assert.Single(section.Groups);
        Assert.Equal("Review overdue: 14/06/2026", draftCard.ReviewStatus);
    }

    [Theory]
    [InlineData(false, "Review complete")]
    [InlineData(true, "Review incomplete")]
    public void GetReviewStatus_WithoutANextReviewDate_SaysWhetherTheReviewIsComplete(bool reviewIncomplete, string expected) =>
        Assert.Equal(expected, SurveillanceProfilesSearchModel.GetReviewStatus(null, reviewIncomplete, Now));

    [Fact]
    public void GetReviewStatus_WithANextReviewDateInThePast_SaysTheReviewIsOverdue() =>
        Assert.Equal(
            "Review overdue: 14/06/2026",
            SurveillanceProfilesSearchModel.GetReviewStatus(new DateTime(2026, 6, 14), reviewIncomplete: true, Now));

    [Fact]
    public void GetReviewStatus_WithANextReviewDateEarlierToday_SaysTheReviewIsOverdue() =>
        Assert.Equal(
            "Review overdue: 15/06/2026",
            SurveillanceProfilesSearchModel.GetReviewStatus(new DateTime(2026, 6, 15), reviewIncomplete: false, Now));

    [Fact]
    public void GetReviewStatus_WithANextReviewDateInTheFuture_SaysWhenTheNextReviewIs() =>
        Assert.Equal(
            "Next review: 16/06/2026",
            SurveillanceProfilesSearchModel.GetReviewStatus(new DateTime(2026, 6, 16), reviewIncomplete: true, Now));

    [Fact]
    public void GetVersionGroups_PublishedCard_ShowsThePublishedStatus_OnlyWhenItHasACurrentVersion()
    {
        var withVersion = SurveillanceProfilesSearchModel.GetVersionGroups(
            Profile("A", published: [Version(1)]), displayPublished: true, displayDraft: false, now: Now);
        var withoutVersion = SurveillanceProfilesSearchModel.GetVersionGroups(
            Profile("A", draft: [Version(1)]), displayPublished: true, displayDraft: false, now: Now);

        var publishedCard = Assert.Single(withVersion);
        Assert.Equal("Published", publishedCard.ProfileStatus);
        Assert.Null(publishedCard.ReviewStatus);
        Assert.Null(Assert.Single(withoutVersion).ProfileStatus);
    }

    [Theory]
    [InlineData("Validation complete", "Validation complete")]
    [InlineData("", "None")]
    public void GetVersionGroups_DraftCard_ShowsTheProfileStatusName_OrNone(string profileStatusName, string expected)
    {
        var profile = Profile("A", draft: [Version(1)]) with { ProfileStatusName = profileStatusName };

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, now: Now);

        Assert.Equal(expected, Assert.Single(groups).ProfileStatus);
    }

    [Fact]
    public void GetVersionGroups_DraftCard_ShowsTheProfileStatusAndReview_EvenWithoutACurrentDraftVersion()
    {
        var profile = Profile("A", published: [Version(1)]) with { ProfileStatusName = "Active" };

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, now: Now);

        var draftCard = Assert.Single(groups);
        Assert.Equal("(No current draft version)", draftCard.NoVersionMessage);
        Assert.Equal("Active", draftCard.ProfileStatus);
        Assert.Equal("Review complete", draftCard.ReviewStatus);
    }

    [Fact]
    public void GetVersionGroups_DraftCard_ShowsWhereTheCurrentDraftStandsInItsReview()
    {
        var profile = Profile("A", draft: [Version(1)]) with { NextReviewDate = new DateTime(2026, 6, 1) };

        var groups = SurveillanceProfilesSearchModel.GetVersionGroups(profile, displayPublished: false, displayDraft: true, now: Now);

        Assert.Equal("Review overdue: 01/06/2026", Assert.Single(groups).ReviewStatus);
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


