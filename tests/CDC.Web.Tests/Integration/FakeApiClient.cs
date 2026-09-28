using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Integration;

// Test double for IApiClient so integration tests can render real search results without a live
// CDC.Api. Only SearchProfilesAsync is exercised by these tests.
internal sealed class FakeApiClient(IReadOnlyList<ProfileSearchResultDto>? searchResults = null) : IApiClient
{
    private readonly IReadOnlyList<ProfileSearchResultDto> results = searchResults ?? [];

    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ApiHealthResponse?>(null);

    public Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(results);
}
