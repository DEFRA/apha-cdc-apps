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
            Scenarios = []
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
}
