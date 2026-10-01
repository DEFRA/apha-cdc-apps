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
    UpdateProfileTitleResult? updateProfileTitleResult = null,
    ManageProfileViewModel? manageProfile = null,
    Exception? throwOnGetManageProfile = null,
    IReadOnlyList<ProfileStatusTypeDto>? profileStatusTypes = null,
    UpdateProfileStatusResult? updateProfileStatusResult = null) : IApiClient
{
    private readonly ApiHealthResponse? _response = response ?? new ApiHealthResponse("Healthy", 1, DateTime.UtcNow);
    private readonly IReadOnlyList<ProfileSearchResultDto> _searchResults = searchResults ?? [];
    private readonly IReadOnlyList<ProfileStatusTypeDto> _profileStatusTypes = profileStatusTypes ?? [];
    private readonly UpdateProfileTitleResult _updateProfileTitleResult =
        updateProfileTitleResult ?? new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null);
    private readonly UpdateProfileStatusResult _updateProfileStatusResult =
        updateProfileStatusResult ?? new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Success, null);

    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        throwOnGetHealth is not null
            ? Task.FromException<ApiHealthResponse?>(throwOnGetHealth)
            : Task.FromResult(_response);

    public Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        SearchForType searchForType,
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

    public Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        throwOnGetManageProfile is not null
            ? Task.FromException<ManageProfileViewModel?>(throwOnGetManageProfile)
            : Task.FromResult(manageProfile);

    public Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_profileStatusTypes);

    public Task<UpdateProfileStatusResult> UpdateProfileStatusAsync(
        Guid profileId,
        Guid profileStatusId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_updateProfileStatusResult);

    public Task<IReadOnlyList<StaticReportListItemDto>> GetCurrentStaticReportsAsync(
        bool isUserManual = false,
        bool publicOnly = true,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StaticReportListItemDto>>([]);
}
