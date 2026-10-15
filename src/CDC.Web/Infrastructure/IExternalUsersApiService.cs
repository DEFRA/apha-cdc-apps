using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Typed client for the external users endpoints on CDC.Api.</summary>
public interface IExternalUsersApiService
{
    /// <summary>Calls <c>GET /api/external-users</c>.</summary>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">Items per page, or 0 for every user on a single page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page of external users.</returns>
    Task<PagedResult<MaintainedExternalUserDto>> GetExternalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/external-users/{id}/review-email-subscription</c>.</summary>
    /// <param name="id">The user being changed.</param>
    /// <param name="subscribed">Whether the user should receive review notification emails.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the change was saved.</returns>
    Task<bool> SetReviewEmailSubscriptionAsync(Guid id, bool subscribed, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/external-users/{id}/profile-access</c>.</summary>
    /// <param name="id">The external user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user's profile access, or <see langword="null"/> when the user does not exist.</returns>
    Task<ExternalUserProfileAccessContract?> GetProfileAccessAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/external-users/{id}/profile-access/{profileId}</c>.</summary>
    /// <param name="id">The external user.</param>
    /// <param name="profileId">The profile being granted.</param>
    /// <param name="request">The role and sections to grant.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the access was saved, with a message when it was not.</returns>
    Task<ExternalUserAccessResult> SaveProfileAccessAsync(
        Guid id, Guid profileId, SaveExternalUserProfileAccessContract request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/external-users/{id}/profile-access/{profileId}</c>.</summary>
    /// <param name="id">The external user.</param>
    /// <param name="profileId">The profile to remove access to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the access was removed.</returns>
    Task<bool> DeleteProfileAccessAsync(Guid id, Guid profileId, CancellationToken cancellationToken = default);
}
