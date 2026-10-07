using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the profile contributors endpoint on CDC.Api.
/// </summary>
public interface IProfileContributorsApiService
{
    /// <summary>Calls <c>GET /api/profiles/{profileId}/contributors</c>.</summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">The number of items per page, or 0 for every contributor on a single page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page of contributors.</returns>
    Task<PagedResult<ContributorDto>> GetProfileContributorsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profiles/{profileId}/contributors/{contributorId}</c>.</summary>
    /// <param name="profileId">The profile the contributor belongs to.</param>
    /// <param name="contributorId">The contributor (user) to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The contributor's editable detail, or <see langword="null"/> when they are not on this profile.</returns>
    Task<ContributorEditDto?> GetContributorForEditAsync(Guid profileId, Guid contributorId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-user-roles</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every contributor role.</returns>
    Task<IReadOnlyList<ProfileUserRoleDto>> GetProfileUserRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/profiles/{profileId}/contributors/{contributorId}</c>.</summary>
    /// <param name="profileId">The profile the contributor belongs to.</param>
    /// <param name="contributorId">The contributor (user) being updated.</param>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<UpdateContributorResult> UpdateContributorAsync(
        Guid profileId,
        Guid contributorId,
        UpdateContributorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-contributors/verify-username</c>.</summary>
    /// <param name="userName">The username to verify.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The verification outcome.</returns>
    Task<UserVerificationResultDto> VerifyContributorUsernameAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profiles/{profileId}/contributors</c>.</summary>
    /// <param name="profileId">The profile to add the contributor to.</param>
    /// <param name="request">The contributor to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<AddContributorResult> AddContributorAsync(Guid profileId, AddContributorRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/profiles/{profileId}/contributors/{contributorId}</c>.</summary>
    /// <param name="profileId">The profile to remove the contributor from.</param>
    /// <param name="contributorId">The contributor (user) being removed.</param>
    /// <param name="lastUpdated">The row version last read for this contributor.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<DeleteContributorResult> DeleteContributorAsync(
        Guid profileId, Guid contributorId, byte[] lastUpdated, CancellationToken cancellationToken = default);
}
