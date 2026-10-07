namespace CDC.Api.Features.ProfileContributors.Interfaces;

using CDC.Api.Features.ProfileContributors.Commands;

/// <summary>
/// Data access for a profile's contributors. Implementations must contain no business logic.
/// </summary>
public interface IProfileContributorsRepository
{
    /// <summary>
    /// Reads every contributor for a profile via <c>spgProfileContributorsByProfileId</c>. The
    /// stored procedure has no paging parameters, so paging is applied afterwards in
    /// <see cref="IProfileContributorsService"/>.
    /// </summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every contributor for the profile; empty when it has none.</returns>
    Task<IReadOnlyList<Domain.Entities.Contributor>> GetProfileContributorsAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>Reads one contributor's full editable detail via <c>spgContributor</c>.</summary>
    /// <param name="profileId">The profile the contributor belongs to.</param>
    /// <param name="contributorId">The contributor (user) to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The contributor's editable detail, or <see langword="null"/> when they are not on this profile.</returns>
    Task<Domain.Entities.ContributorEdit?> GetContributorForEditAsync(Guid profileId, Guid contributorId, CancellationToken cancellationToken);

    /// <summary>Reads every contributor role via <c>spgaluProfileUserRole</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every role a contributor can hold.</returns>
    Task<IReadOnlyList<Domain.Entities.ProfileUserRole>> GetProfileUserRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies a contributor update transactionally: the row version check and role/full
    /// name/organisation upsert (<c>spiProfileContributor</c>), then the section permission
    /// diff (<c>spiProfileSectionUser</c>/<c>spdProfileSectionUser</c>).
    /// </summary>
    /// <param name="command">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new row version, or <see langword="null"/> when the contributor does not exist.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task<byte[]?> UpdateContributorAsync(UpdateContributorCommand command, CancellationToken cancellationToken);

    /// <summary>Looks up a global user by username via <c>spgUserAuthorisation</c>.</summary>
    /// <param name="userName">The username to look up.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching global user, or <see langword="null"/> when no such username exists.</returns>
    Task<Domain.Entities.UserVerification?> FindUserByUsernameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new profile contributor transactionally, upserting the <c>User</c>/<c>ProfileUser</c>
    /// row (<c>spiProfileContributor</c> - inserts a brand-new global user when
    /// <see cref="AddContributorCommand.ContributorId"/> does not yet exist), then granting
    /// every requested section permission (<c>spiProfileSectionUser</c>).
    /// </summary>
    /// <param name="command">The contributor to add.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when an existing global user's row version has moved on since it was read.
    /// </exception>
    /// <exception cref="Domain.Exceptions.DuplicateUsernameException">
    /// Thrown when the username is already in use by another global user.
    /// </exception>
    Task AddContributorAsync(AddContributorCommand command, CancellationToken cancellationToken);

    /// <summary>Removes a contributor from a profile via <c>spdProfileContributor</c>.</summary>
    /// <param name="profileId">The profile to remove the contributor from.</param>
    /// <param name="contributorId">The contributor (user) being removed.</param>
    /// <param name="lastUpdated">The row version last read for this contributor.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the supplied row version no longer matches the stored value.
    /// </exception>
    Task DeleteContributorAsync(Guid profileId, Guid contributorId, byte[] lastUpdated, CancellationToken cancellationToken);
}
