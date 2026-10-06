using CDC.Api.Features.UserAdmin.Commands;
using CDC.Api.Features.UserAdmin.Dtos;

namespace CDC.Api.Features.UserAdmin.Interfaces;

/// <summary>
/// Application service behind the Maintain global users and Maintain external users screens.
/// </summary>
public interface IUserAdminService
{
    /// <summary>Gets every internal (global) user account.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The global users, ordered by full name.</returns>
    Task<IReadOnlyList<MaintainedUserDto>> GetGlobalUsersAsync(CancellationToken cancellationToken);

    /// <summary>Gets every external (single sign-on) user account.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The external users, ordered by user name.</returns>
    Task<IReadOnlyList<MaintainedUserDto>> GetExternalUsersAsync(CancellationToken cancellationToken);

    /// <summary>Gets one user account.</summary>
    /// <param name="userId">The user to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<MaintainedUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Subscribes a user to, or unsubscribes them from, review notification emails.</summary>
    /// <param name="command">The user and the subscription state to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new subscription state and row version, or <see langword="null"/> when no such user exists.</returns>
    Task<UpdateReviewEmailSubscriptionResultDto?> UpdateReviewEmailSubscriptionAsync(
        UpdateReviewEmailSubscriptionCommand command,
        CancellationToken cancellationToken);
}
