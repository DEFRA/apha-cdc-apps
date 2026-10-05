using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api, so the search results partial
// only ever renders its empty-results path. This swaps in fakes with real data so the results
// table (and its version-history rendering) actually executes.
public class SurveillanceProfilesSearchIntegrationTests
{
    private static readonly Guid ProfileId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid VersionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(SearchResults));
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService([]));
            }));
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SurveillanceProfiles/Search");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Bovine tuberculosis", body);
    }

    [Fact]
    public async Task Search_ResolvesSelectedSpeciesLabel_FromNestedTreeNode()
    {
        var cattleId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
        var dairyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = cattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = dairyId, ParentId = cattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true }
        ];
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(SearchResults));
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService(species));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/Search?SelectedSpecies={dairyId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Dairy cattle", body);
    }

    [Fact]
    public async Task Search_FallsBackToAnySpecies_WhenSelectedSpeciesIdIsUnknown()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(SearchResults));
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService([]));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/Search?SelectedSpecies={Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Any species", body);
    }

    [Fact]
    public async Task Search_RendersPreviousVersionsAndDraftCard_WhenProfileHasMultipleVersions()
    {
        var olderPublishedVersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var draftVersionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        IReadOnlyList<ProfileSearchResultDto> searchResults =
        [
            SearchResults[0] with
            {
                PublishedVersions =
                [
                    SearchResults[0].PublishedVersions[0],
                    new ProfileHistoryItemDto
                    {
                        VersionId = olderPublishedVersionId,
                        VersionNumber = 0,
                        Title = "Bovine tuberculosis",
                        CreatedAtUtc = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        IsScenario = false
                    }
                ],
                DraftVersions =
                [
                    new ProfileHistoryItemDto
                    {
                        VersionId = draftVersionId,
                        VersionNumber = 2,
                        Title = "Bovine tuberculosis",
                        CreatedAtUtc = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                        IsScenario = false
                    }
                ]
            }
        ];
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(searchResults));
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService([]));
            }));
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/SurveillanceProfiles/Search?DisplayDraft=true");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Show previous versions", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Draft current version", body, StringComparison.OrdinalIgnoreCase);
    }
}
