using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.UserAdmin.Interfaces;

/// <summary>
/// Data access for the user administration screens.
/// </summary>
public interface IUserAdminRepository
{
    /// <summary>Reads every internal (global) user account.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The global users, ordered by full name.</returns>
    Task<IReadOnlyList<MaintainedUser>> GetGlobalUsersAsync(CancellationToken cancellationToken);

    /// <summary>Reads every external (single sign-on) user account.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The external users, ordered by user name.</returns>
    Task<IReadOnlyList<MaintainedUser>> GetExternalUsersAsync(CancellationToken cancellationToken);

    /// <summary>Reads one user account.</summary>
    /// <param name="userId">The user to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<MaintainedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a user's review notification email subscription, leaving every other attribute
    /// of the account unchanged.
    /// </summary>
    /// <param name="userId">The user to update.</param>
    /// <param name="subscribed"><see langword="true"/> to subscribe, <see langword="false"/> to unsubscribe.</param>
    /// <param name="lastUpdated">The row version read with the user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user's new row version, or <see langword="null"/> when no such user exists.</returns>
    Task<byte[]?> UpdateReviewEmailSubscriptionAsync(
        Guid userId,
        bool subscribed,
        byte[] lastUpdated,
        CancellationToken cancellationToken);
}
