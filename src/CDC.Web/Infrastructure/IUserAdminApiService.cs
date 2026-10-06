using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the user administration endpoints on CDC.Api.
/// </summary>
public interface IUserAdminApiService
{
    /// <summary>Gets every internal (global) user account.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The global users.</returns>
    Task<IReadOnlyList<MaintainedUserDto>> GetGlobalUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets every external (single sign-on) user account.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The external users.</returns>
    Task<IReadOnlyList<MaintainedUserDto>> GetExternalUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets one user account.</summary>
    /// <param name="userId">The user to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<MaintainedUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Subscribes a user to, or unsubscribes them from, review notification emails.</summary>
    /// <param name="request">The user and the subscription state to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the update.</returns>
    Task<UpdateReviewEmailSubscriptionResult> UpdateReviewEmailSubscriptionAsync(
        UpdateReviewEmailSubscriptionRequest request,
        CancellationToken cancellationToken = default);
}
