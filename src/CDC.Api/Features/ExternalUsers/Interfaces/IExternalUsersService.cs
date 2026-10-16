using CDC.Api.Domain.Common;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Dtos;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ExternalUsers.Interfaces;

/// <summary>Application service for maintaining external (SSO) users.</summary>
public interface IExternalUsersService
{
    /// <summary>Gets one page of external users.</summary>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">Items per page, or 0 for every user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The requested page.</returns>
    Task<PagedResult<ExternalUserDto>> GetExternalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>Subscribes or unsubscribes a user from review notification emails.</summary>
    /// <param name="command">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or not found when the user does not exist.</returns>
    Task<Result<Unit>> SetReviewEmailSubscriptionAsync(SetExternalUserSubscriptionCommand command, CancellationToken cancellationToken);

    /// <summary>Gets the profiles an external user can access, and the profiles still available to grant.</summary>
    /// <param name="userId">The external user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile access, or not found when the user is not an external user.</returns>
    Task<Result<ExternalUserProfileAccessContract>> GetProfileAccessAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Grants an external user a role and section permissions on a profile, or changes an existing grant.</summary>
    /// <param name="command">The access to save.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the validation, conflict or not-found failure.</returns>
    Task<Result<Unit>> SaveProfileAccessAsync(SaveExternalUserProfileAccessCommand command, CancellationToken cancellationToken);

    /// <summary>Removes an external user's access to a profile.</summary>
    /// <param name="command">The access to remove.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the conflict or not-found failure.</returns>
    Task<Result<Unit>> DeleteProfileAccessAsync(DeleteExternalUserProfileAccessCommand command, CancellationToken cancellationToken);
}
