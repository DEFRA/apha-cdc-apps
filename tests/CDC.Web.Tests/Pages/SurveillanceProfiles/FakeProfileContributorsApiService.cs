using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// Test double for IProfileContributorsApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeProfileContributorsApiService(
    PagedResult<ContributorDto>? result = null,
    Exception? throwOnGetProfileContributors = null,
    ContributorEditDto? contributorForEdit = null,
    IReadOnlyList<ProfileUserRoleDto>? profileUserRoles = null,
    UpdateContributorResult? updateContributorResult = null,
    Exception? throwOnUpdateContributor = null,
    Exception? throwOnGetContributorForEdit = null,
    UserVerificationResultDto? verificationResult = null,
    Exception? throwOnVerifyContributorUsername = null,
    AddContributorResult? addContributorResult = null,
    Exception? throwOnAddContributor = null,
    DeleteContributorResult? deleteContributorResult = null,
    Exception? throwOnDeleteContributor = null)
    : IProfileContributorsApiService
{
    /// <summary>Gets the page size most recently passed to <see cref="GetProfileContributorsAsync"/>.</summary>
    public int? RequestedPageSize { get; private set; }

    /// <summary>Gets the request most recently passed to <see cref="UpdateContributorAsync"/>.</summary>
    public UpdateContributorRequest? LastUpdateRequest { get; private set; }

    /// <summary>Gets the request most recently passed to <see cref="AddContributorAsync"/>.</summary>
    public AddContributorRequest? LastAddRequest { get; private set; }

    public Task<PagedResult<ContributorDto>> GetProfileContributorsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequestedPageSize = pageSize;

        return throwOnGetProfileContributors is not null
            ? Task.FromException<PagedResult<ContributorDto>>(throwOnGetProfileContributors)
            : Task.FromResult(result ?? new PagedResult<ContributorDto>
            {
                Items = [],
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = 0
            });
    }

    public Task<ContributorEditDto?> GetContributorForEditAsync(
        Guid profileId,
        Guid contributorId,
        CancellationToken cancellationToken = default) =>
        throwOnGetContributorForEdit is not null
            ? Task.FromException<ContributorEditDto?>(throwOnGetContributorForEdit)
            : Task.FromResult(contributorForEdit);

    public Task<IReadOnlyList<ProfileUserRoleDto>> GetProfileUserRolesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(profileUserRoles ?? []);

    public Task<UpdateContributorResult> UpdateContributorAsync(
        Guid profileId,
        Guid contributorId,
        UpdateContributorRequest request,
        CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = request;

        return throwOnUpdateContributor is not null
            ? Task.FromException<UpdateContributorResult>(throwOnUpdateContributor)
            : Task.FromResult(updateContributorResult ?? new UpdateContributorResult { Outcome = ContributorUpdateOutcome.Success });
    }

    public Task<UserVerificationResultDto> VerifyContributorUsernameAsync(string userName, CancellationToken cancellationToken = default) =>
        throwOnVerifyContributorUsername is not null
            ? Task.FromException<UserVerificationResultDto>(throwOnVerifyContributorUsername)
            : Task.FromResult(verificationResult ?? new UserVerificationResultDto { Outcome = UserVerificationOutcome.InvalidFormat });

    public Task<AddContributorResult> AddContributorAsync(
        Guid profileId, AddContributorRequest request, CancellationToken cancellationToken = default)
    {
        LastAddRequest = request;

        return throwOnAddContributor is not null
            ? Task.FromException<AddContributorResult>(throwOnAddContributor)
            : Task.FromResult(addContributorResult ?? new AddContributorResult { Outcome = ContributorAddOutcome.Success });
    }

    public Task<DeleteContributorResult> DeleteContributorAsync(
        Guid profileId, Guid contributorId, byte[] lastUpdated, CancellationToken cancellationToken = default) =>
        throwOnDeleteContributor is not null
            ? Task.FromException<DeleteContributorResult>(throwOnDeleteContributor)
            : Task.FromResult(deleteContributorResult ?? new DeleteContributorResult { Outcome = ContributorDeleteOutcome.Success });
}
