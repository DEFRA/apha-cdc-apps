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
        SearchForType searchForType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(results);

    public Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ProfileAttributesDto?>(null);

    public Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Error, "Not implemented in this fake."));

    public Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ManageProfileViewModel?>(null);

    public Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProfileStatusTypeDto>>([]);

    public Task<UpdateProfileStatusResult> UpdateProfileStatusAsync(
        Guid profileId,
        Guid profileStatusId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Error, "Not implemented in this fake."));

    public Task<IReadOnlyList<StaticReportListItemDto>> GetCurrentStaticReportsAsync(
        bool isUserManual = false,
        bool publicOnly = true,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StaticReportListItemDto>>([]);

    public Task<CreateNewProfileVersionResult> CreateNewProfileVersionAsync(
        Guid profileVersionId,
        bool isPublished,
        bool isPublic,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Error, null, "Not implemented in this fake."));

    public Task<DeleteProfileVersionResult> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Error, false, "Not implemented in this fake."));
}
