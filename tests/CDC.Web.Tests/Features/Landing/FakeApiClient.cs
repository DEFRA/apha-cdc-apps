using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Features.Landing;

// Test double for IApiClient so controller/health-check unit tests don't need a real HTTP call.
internal sealed class FakeApiClient(
    ApiHealthResponse? response = null,
    Exception? throwOnGetHealth = null,
    IReadOnlyList<ProfileSearchResultDto>? searchResults = null,
    Exception? throwOnSearchProfiles = null,
    ProfileAttributesDto? profileAttributes = null,
    Exception? throwOnGetProfileAttributes = null,
    UpdateProfileTitleResult? updateProfileTitleResult = null) : IApiClient
{
    private readonly ApiHealthResponse? _response = response ?? new ApiHealthResponse("Healthy", 1, DateTime.UtcNow);
    private readonly IReadOnlyList<ProfileSearchResultDto> _searchResults = searchResults ?? [];
    private readonly UpdateProfileTitleResult _updateProfileTitleResult =
        updateProfileTitleResult ?? new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null);

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

    public Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        throwOnGetProfileAttributes is not null
            ? Task.FromException<ProfileAttributesDto?>(throwOnGetProfileAttributes)
            : Task.FromResult(profileAttributes);

    public Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_updateProfileTitleResult);
}
