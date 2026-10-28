using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Integration;

// Test double for IApiClient so integration tests can render real search results without a live
// CDC.Api. Only SearchProfilesAsync is exercised by these tests.
internal sealed class FakeApiClient(
    IReadOnlyList<ProfileSearchResultDto>? searchResults = null,
    ProfileAttributesDto? profileAttributes = null,
    Exception? searchFailure = null,
    Exception? attributesFailure = null,
    int searchesBeforeFailure = 0) : IApiClient
{
    private int searchCalls;

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
        searchFailure is null || searchCalls++ < searchesBeforeFailure ? Task.FromResult(results) : Task.FromException<IReadOnlyList<ProfileSearchResultDto>>(searchFailure);

    public Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        attributesFailure is null
            ? Task.FromResult(profileAttributes?.Id == profileId ? profileAttributes : null)
            : Task.FromException<ProfileAttributesDto?>(attributesFailure);

    public Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Error, "Not implemented in this fake."));

    public Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ManageProfileViewModel?>(null);

    public Task<ResolveExternalUserResult> ResolveExternalUserAsync(
        ResolveExternalUserRequestDto request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

    public Task<ResolveInternalUserResult> ResolveInternalUserAsync(
        ResolveInternalUserRequestDto request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

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

    public Task<IReadOnlyList<StaticReportListItemDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly = true,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StaticReportListItemDto>>([]);

    public Task<bool> CanUploadStaticReportsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<UploadStaticReportResult> UploadStaticReportAsync(
        string title,
        byte[] pdfData,
        bool isUserManual,
        bool isPublic,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UploadStaticReportResult(UploadStaticReportOutcome.Forbidden, "Not implemented in this fake."));

    public Task<bool> SetStaticReportPublicAccessAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<DeleteStaticReportVersionResult> DeleteStaticReportVersionAsync(
        Guid staticReportVersionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeleteStaticReportVersionResult(DeleteStaticReportVersionOutcome.Error, "Not implemented in this fake."));

    public Task<CreateNewProfileVersionResult> CreateNewProfileVersionAsync(
        Guid profileVersionId,
        bool isPublished,
        bool isPublic,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Error, null, "Not implemented in this fake."));

    public Task<PublishProfileResult> PublishProfileAsync(Guid profileId, bool isPublic, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PublishProfileResult(PublishProfileOutcome.Error, null, "Not implemented in this fake."));

    public Task<DeleteProfileVersionResult> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Error, false, "Not implemented in this fake."));
}
