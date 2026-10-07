using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Interfaces;

/// <summary>
/// Application service for the profile contributors feature. Owns the mapping between domain
/// entities and the DTOs exposed over HTTP, and the paging applied over the full contributor
/// list, so MediatR handlers stay thin.
/// </summary>
public interface IProfileContributorsService
{
    /// <summary>Gets one page of a profile's contributors.</summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="pageNumber">The 1-based page to return. Clamped to at least 1.</param>
    /// <param name="pageSize">The number of items per page, or 0/negative to return every
    /// contributor on a single page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The requested page of contributors.</returns>
    Task<PagedResult<ContributorDto>> GetProfileContributorsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Gets one contributor's full editable detail.</summary>
    /// <param name="profileId">The profile the contributor belongs to.</param>
    /// <param name="contributorId">The contributor (user) to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The contributor's editable detail, or <see langword="null"/> when they are not on this profile.</returns>
    Task<ContributorEditDto?> GetContributorForEditAsync(Guid profileId, Guid contributorId, CancellationToken cancellationToken);

    /// <summary>Gets every role a contributor can hold on a profile.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every contributor role.</returns>
    Task<IReadOnlyList<ProfileUserRoleDto>> GetProfileUserRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates and applies a contributor update: a role must be selected, a contributor role
    /// requires at least one section permission and a reviewer role requires none, and (for
    /// non-SSO users) full name and organisation are required.
    /// </summary>
    /// <param name="command">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the business validation/concurrency/not-found failure.</returns>
    Task<Result<Unit>> UpdateContributorAsync(UpdateContributorCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a username for the "Add profile contributor" lookup step: whether it matches an
    /// existing global user (and whether they may be made a contributor), a validly-formatted new
    /// username, or neither.
    /// </summary>
    /// <param name="userName">The username to verify.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The verification outcome.</returns>
    Task<UserVerificationResultDto> VerifyUsernameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>
    /// Validates and applies a new contributor: the same role/section-permission/full-name/
    /// organisation rules as <see cref="UpdateContributorAsync"/>, plus a duplicate-username check
    /// when the contributor is a brand-new global user.
    /// </summary>
    /// <param name="command">The contributor to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the business validation/concurrency failure.</returns>
    Task<Result<Unit>> AddContributorAsync(AddContributorCommand command, CancellationToken cancellationToken);

    /// <summary>Removes a contributor from a profile.</summary>
    /// <param name="command">The contributor to remove.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the concurrency failure.</returns>
    Task<Result<Unit>> DeleteContributorAsync(DeleteContributorCommand command, CancellationToken cancellationToken);
}
