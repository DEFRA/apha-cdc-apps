using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Features.Landing;

// Test double for IApiClient so controller/health-check unit tests don't need a real HTTP call.
internal sealed class FakeApiClient(ApiHealthResponse? response = null, Exception? throwOnGetHealth = null) : IApiClient
{
    private readonly ApiHealthResponse? _response = response ?? new ApiHealthResponse("Healthy", 1, DateTime.UtcNow);

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
        Task.FromResult<IReadOnlyList<ProfileSearchResultDto>>([]);
}
