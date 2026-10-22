using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api, so the search results partial
// only ever renders its empty-results path. This swaps in fakes with real data so the results
// table (and its version-history rendering) actually executes. Uses CdcWebTestFactory (not the
// plain WebApplicationFactory<Program>) since this route requires authentication, and signs in
// via the test-only endpoint before the actual request.
public class SurveillanceProfilesSearchIntegrationTests
{
    private static readonly Guid ProfileId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid VersionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ScenarioId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly IReadOnlyList<ProfileSearchResultDto> SearchResults =
    [
        new ProfileSearchResultDto
        {
            Id = ProfileId,
            Title = "Bovine tuberculosis",
            Status = "Published",
            CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            IsPublic = true,
            AffectedSpecies = ["Cattle"],
            PublishedVersions =
            [
                new ProfileHistoryItemDto
                {
                    VersionId = VersionId,
                    VersionNumber = 1,
                    Title = "Bovine tuberculosis",
                    CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsScenario = false
                }
            ],
            DraftVersions = [],
            WhatIfScenarios = []
        }
    ];

    [Fact]
    public async Task Search_RendersResultsPartial_ForAjaxRequest()
    {
        using var factory = new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(SearchResults));
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService([]));
            }));
        var client = factory.CreateClient();
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SurveillanceProfiles/Search");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Bovine tuberculosis", body);
    }

    // Legacy Search.aspx showed "Edit Title" only when Profile.CanEditProfile(); the page refuses everyone else.
    [Fact]
    public async Task Search_ShowsTheEditTitleLink_ToAProfileEditor()
    {
        var body = await GetResultsPartialAsync(isProfileEditor: true, isPolicyProfileUser: false);

        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileTitle/{ProfileId}\">Edit Title</a>", body);
        Assert.Contains(">Manage</a>", body);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Search_HidesTheEditTitleLink_FromAUserWhoMayNotEditAProfile(bool isProfileEditor, bool isPolicyProfileUser)
    {
        var body = await GetResultsPartialAsync(isProfileEditor, isPolicyProfileUser);

        Assert.Contains("Bovine tuberculosis", body);
        Assert.DoesNotContain("Edit Title", body);
        Assert.DoesNotContain("EditProfileTitle", body);
        Assert.Contains(">Manage</a>", body);
    }

    // The script submits the form for these links, so the address they carry cannot bring back a keyword cleared since the last search.
    [Fact]
    public async Task Search_MarksEveryLetterLinkAndPageLink_ForTheScriptToSubmitTheFilters()
    {
        var profiles = Enumerable.Range(1, 11)
            .Select(number => SearchResults[0] with { Id = Guid.NewGuid(), Title = $"Profile {number:00}" })
            .ToList();
        using var factory = CreateFactory(profiles);
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/SurveillanceProfiles/Search");

        Assert.Contains("data-app-search-letter=\"All\"", page);
        Assert.All("ABCDEFGHIJKLMNOPQRSTUVWXYZ", letter => Assert.Contains($"data-app-search-letter=\"{letter}\"", page));
        Assert.Contains("<input type=\"hidden\" id=\"PageNumber\" name=\"PageNumber\" value=\"1\" form=\"search-form\" />", page);
        Assert.Contains("data-app-search-page=\"1\" aria-label=\"Page 1\"", page);
        Assert.Contains("data-app-search-page=\"2\" rel=\"next\"", page);
    }

    // Legacy listed every scenario under its own title with its own Manage link, and moved those links out of the profile header.
    [Fact]
    public async Task Search_WithScenariosDisplayed_ListsEachScenarioUnderItsTitle_WithItsOwnManageLink()
    {
        var body = await GetResultsPartialAsync(
            ResultsWithAScenario(), "?DisplayPublished=true&DisplayDraft=true&DisplayScenarios=true");

        Assert.Contains(">Current situation</h4>", body);
        Assert.Contains(">Wildlife scenario</h4>", body);
        Assert.Contains($"/SurveillanceProfiles/ManageProfile/{ProfileId}\">Manage<span class=\"govuk-visually-hidden\"> Current situation</span></a>", body);
        Assert.Contains($"/SurveillanceProfiles/ManageProfile/{ScenarioId}\">Manage<span class=\"govuk-visually-hidden\"> Wildlife scenario</span></a>", body);
        Assert.DoesNotContain(">Manage</a>", body);
        Assert.Contains("EditProfileTitle", body);
    }

    [Fact]
    public async Task Search_WithoutScenariosDisplayed_KeepsManageInTheProfileHeader_AndListsNoScenarios()
    {
        var body = await GetResultsPartialAsync(ResultsWithAScenario(), "?DisplayPublished=true&DisplayDraft=true");

        Assert.Contains($"/SurveillanceProfiles/ManageProfile/{ProfileId}\">Manage</a>", body);
        Assert.DoesNotContain("</h4>", body);
        Assert.DoesNotContain("Wildlife scenario", body);
    }

    // The scenarios-only case is refused before the API is called, so no profile is listed.
    [Fact]
    public async Task Search_WithOnlyScenariosDisplayed_ListsNoProfiles()
    {
        var body = await GetResultsPartialAsync(
            ResultsWithAScenario(), "?DisplayPublished=false&DisplayDraft=false&DisplayScenarios=true");

        Assert.DoesNotContain("Bovine tuberculosis", body);
        Assert.DoesNotContain("Wildlife scenario", body);
    }

    private static IReadOnlyList<ProfileSearchResultDto> ResultsWithAScenario() =>
    [
        SearchResults[0] with
        {
            ScenarioTitle = "Current situation",
            WhatIfScenarios =
            [
                new ProfileScenarioDto
                {
                    ScenarioId = ScenarioId,
                    ScenarioTitle = "Wildlife scenario",
                    PublishedVersions = [],
                    DraftVersions =
                    [
                        new ProfileHistoryItemDto
                        {
                            VersionId = Guid.NewGuid(),
                            VersionNumber = 1,
                            Title = "Bovine tuberculosis",
                            CreatedAtUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                            IsScenario = true
                        }
                    ]
                }
            ]
        }
    ];

    private static async Task<string> GetResultsPartialAsync(IReadOnlyList<ProfileSearchResultDto> searchResults, string query)
    {
        using var factory = CreateFactory(searchResults);
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor: true, isPolicyProfileUser: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/SurveillanceProfiles/Search{query}");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(IReadOnlyList<ProfileSearchResultDto> searchResults) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(searchResults));
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService([]));
            }));

    private static async Task<string> GetResultsPartialAsync(bool isProfileEditor, bool isPolicyProfileUser)
    {
        using var factory = CreateFactory(SearchResults);
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor, isPolicyProfileUser);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SurveillanceProfiles/Search");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        return await response.Content.ReadAsStringAsync();
    }
}
