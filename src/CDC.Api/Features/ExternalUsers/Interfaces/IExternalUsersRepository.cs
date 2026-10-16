using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.ExternalUsers.Interfaces;

/// <summary>Data access for maintaining external (SSO) users.</summary>
public interface IExternalUsersRepository
{
    /// <summary>Gets every external user, ordered by username.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The external users.</returns>
    Task<IReadOnlyList<ExternalUserAccount>> GetExternalUsersAsync(CancellationToken cancellationToken);

    /// <summary>Gets the profiles one external user can access, and the profiles still available to grant.</summary>
    /// <param name="userId">The external user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user's profile access.</returns>
    Task<ExternalUserProfileAccess> GetProfileAccessAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Saves the user, including their review email subscription.</summary>
    /// <param name="user">The user to save.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the user is saved.</returns>
    Task SaveExternalUserAsync(ExternalUserAccount user, CancellationToken cancellationToken);
}
