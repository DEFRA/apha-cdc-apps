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

    /// <summary>Creates an external user that has not yet signed in through CIDM.</summary>
    /// <param name="id">The new user's identifier.</param>
    /// <param name="userName">The logon username.</param>
    /// <param name="fullName">The user's full name.</param>
    /// <param name="organisation">The user's organisation.</param>
    /// <param name="emailAddress">The user's email address.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when created; <see langword="false"/> when the email address or user name is already in use.</returns>
    Task<bool> PreRegisterExternalUserAsync(
        Guid id, string userName, string fullName, string organisation, string emailAddress, CancellationToken cancellationToken);

    /// <summary>Saves the user, including their review email subscription.</summary>
    /// <param name="user">The user to save.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the user is saved.</returns>
    Task SaveExternalUserAsync(ExternalUserAccount user, CancellationToken cancellationToken);

    /// <summary>Gets the identifiers of the external users whose login instructions have not been sent yet.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The identifiers of the users still awaiting their instructions.</returns>
    Task<IReadOnlySet<Guid>> GetUsersAwaitingLoginInstructionsAsync(CancellationToken cancellationToken);

    /// <summary>Gets one external user's login instructions details.</summary>
    /// <param name="userId">The external user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The details, or <see langword="null"/> when the user is not an external user.</returns>
    Task<ExternalUserLoginRecipient?> GetLoginRecipientAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records that the login instructions were sent to an email address.</summary>
    /// <param name="userId">The external user.</param>
    /// <param name="emailAddress">The address the instructions were sent to.</param>
    /// <param name="sentDate">When the instructions were sent, in UTC.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="false"/> when the user's saved address is no longer <paramref name="emailAddress"/>, so nothing was recorded.</returns>
    Task<bool> MarkLoginInstructionsSentAsync(Guid userId, string emailAddress, DateTime sentDate, CancellationToken cancellationToken);
}
