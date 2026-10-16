using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.GlobalUsers.Interfaces;

/// <summary>
/// Data access for global users. Backed by the same stored procedures the legacy
/// <c>GlobalUserList</c> CSLA object used.
/// </summary>
public interface IGlobalUsersRepository
{
    /// <summary>Reads every user holding a global role via <c>spgaGlobalUser</c>, ordered by full name.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every global user; empty when there are none.</returns>
    Task<IReadOnlyList<GlobalUser>> GetGlobalUsersAsync(CancellationToken cancellationToken);

    /// <summary>Reads one user via <c>spgUser</c>.</summary>
    /// <param name="userId">The user to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<GlobalUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Looks a user up by username via <c>spgUserAuthorisation</c>.</summary>
    /// <param name="userName">The username to look up.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching user, or <see langword="null"/> when no such username exists.</returns>
    Task<UserVerification?> FindUserByUsernameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Inserts or updates a user's global role and details via <c>spiGlobalUser</c>.</summary>
    /// <param name="user">The user to save. <see cref="GlobalUser.LastUpdated"/> is the row version read earlier.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">Thrown when the row version no longer matches.</exception>
    /// <exception cref="Domain.Exceptions.DuplicateUsernameException">Thrown when a new user's username is already in use.</exception>
    Task SaveGlobalUserAsync(GlobalUser user, CancellationToken cancellationToken);

    /// <summary>Revokes a user's global role via <c>spdGlobalUser</c>.</summary>
    /// <param name="userId">The user whose role is revoked.</param>
    /// <param name="lastUpdated">The row version last read for this user.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">Thrown when the row version no longer matches.</exception>
    Task DeleteGlobalUserAsync(Guid userId, byte[] lastUpdated, CancellationToken cancellationToken);
}
