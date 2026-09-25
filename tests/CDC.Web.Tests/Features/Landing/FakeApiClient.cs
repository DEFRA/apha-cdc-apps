using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Features.Landing;

// Test double for IApiClient so controller/health-check unit tests don't need a real HTTP call.
internal sealed class FakeApiClient(
    ApiHealthResponse? response = null,
    Exception? throwOnGetHealth = null,
    IReadOnlyList<ProfileSearchResultDto>? searchResults = null,
    Exception? throwOnSearchProfiles = null) : IApiClient
{
    private readonly ApiHealthResponse? _response = response ?? new ApiHealthResponse("Healthy", 1, DateTime.UtcNow);
    private readonly IReadOnlyList<ProfileSearchResultDto> _searchResults = searchResults ?? [];

    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        throwOnGetHealth is not null
            ? Task.FromException<ApiHealthResponse?>(throwOnGetHealth)
            : Task.FromResult(_response);

    public Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        CancellationToken cancellationToken = default) =>
        throwOnSearchProfiles is not null
            ? Task.FromException<IReadOnlyList<ProfileSearchResultDto>>(throwOnSearchProfiles)
            : Task.FromResult(_searchResults);
}
