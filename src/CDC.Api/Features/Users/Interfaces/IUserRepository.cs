namespace CDC.Api.Features.Users.Interfaces;

/// <summary>
/// Data access for CIDM external-user rows. Maps directly onto the <c>spg*</c>/<c>spi*</c>/
/// <c>spu*</c> stored procedures; implementations must contain no business logic - the
/// SsoUserIdExt-then-email resolution order and the external/not-external decision live in
/// <see cref="IUserService"/>.
/// </summary>
public interface IUserRepository
{
    /// <summary>Reads the user row matching a CIDM 'sub' claim via <c>spgUserBySsoUserIdExt</c>.</summary>
    /// <param name="ssoUserIdExt">The CIDM 'sub' claim from the validated id_token.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching row, or <see langword="null"/> when none exists.</returns>
    Task<Domain.Entities.ExternalUser?> GetBySsoUserIdExtAsync(Guid ssoUserIdExt, CancellationToken cancellationToken);

    /// <summary>Reads the user row matching an email address via <c>spgUserByEmailAddress</c>.</summary>
    /// <param name="email">The email claim from the validated id_token.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching row, or <see langword="null"/> when none exists.</returns>
    Task<Domain.Entities.ExternalUser?> GetByEmailAddressAsync(string email, CancellationToken cancellationToken);

    /// <summary>Backfills SsoUserIdExt on a row previously matched by email, via <c>spuUserSsoUserIdExt</c>.</summary>
    /// <param name="id">The row to update.</param>
    /// <param name="ssoUserIdExt">The CIDM 'sub' claim to record.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateSsoUserIdExtAsync(Guid id, Guid ssoUserIdExt, CancellationToken cancellationToken);

    /// <summary>Inserts a brand-new external user row via <c>spiExternalUser</c>.</summary>
    /// <param name="newUser">The row to insert, including a client-assigned Id.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The same row passed in, once persisted.</returns>
    Task<Domain.Entities.ExternalUser> CreateExternalUserAsync(Domain.Entities.ExternalUser newUser, CancellationToken cancellationToken);

    /// <summary>Reads the user row matching an Entra ID 'oid' claim via <c>spgUserBySsoUserIdInt</c>.</summary>
    /// <param name="ssoUserIdInt">The Entra ID 'oid' claim from the validated id_token.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching row, or <see langword="null"/> when none exists.</returns>
    Task<Domain.Entities.InternalUser?> GetBySsoUserIdIntAsync(Guid ssoUserIdInt, CancellationToken cancellationToken);

    /// <summary>Reads the user row matching a user name via <c>spgUserByUserName</c>.</summary>
    /// <param name="userName">The Windows-style user name built from Entra ID on-premises claims.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching row, or <see langword="null"/> when none exists.</returns>
    Task<Domain.Entities.InternalUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Backfills SsoUserIdInt on a row previously matched by user name, via <c>spuUserSsoUserIdInt</c>.</summary>
    /// <param name="id">The row to update.</param>
    /// <param name="ssoUserIdInt">The Entra ID 'oid' claim to record.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateSsoUserIdIntAsync(Guid id, Guid ssoUserIdInt, CancellationToken cancellationToken);
}
