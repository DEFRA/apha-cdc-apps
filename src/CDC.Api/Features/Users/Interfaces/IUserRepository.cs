namespace CDC.Api.Features.Users.Interfaces;

/// <summary>
/// Data access for CIDM external-user rows. Maps directly onto the <c>spg*</c>/<c>spi*</c>/
/// <c>spu*</c> stored procedures; implementations must contain no business logic - the
/// CidmSsoId-then-email resolution order and the external/not-external decision live in
/// <see cref="IUserService"/>.
/// </summary>
public interface IUserRepository
{
    /// <summary>Reads the user row matching a CIDM 'sub' claim via <c>spgUserByCidmSsoId</c>.</summary>
    /// <param name="cidmSsoId">The CIDM 'sub' claim from the validated id_token.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching row, or <see langword="null"/> when none exists.</returns>
    Task<Domain.Entities.ExternalUser?> GetByCidmSsoIdAsync(Guid cidmSsoId, CancellationToken cancellationToken);

    /// <summary>Reads the user row matching an email address via <c>spgUserByEmailAddress</c>.</summary>
    /// <param name="email">The email claim from the validated id_token.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching row, or <see langword="null"/> when none exists.</returns>
    Task<Domain.Entities.ExternalUser?> GetByEmailAddressAsync(string email, CancellationToken cancellationToken);

    /// <summary>Backfills CidmSsoId on a row previously matched by email, via <c>spuUserCidmSsoId</c>.</summary>
    /// <param name="id">The row to update.</param>
    /// <param name="cidmSsoId">The CIDM 'sub' claim to record.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UpdateCidmSsoIdAsync(Guid id, Guid cidmSsoId, CancellationToken cancellationToken);

    /// <summary>Inserts a brand-new external user row via <c>spiExternalUser</c>.</summary>
    /// <param name="newUser">The row to insert, including a client-assigned Id.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The same row passed in, once persisted.</returns>
    Task<Domain.Entities.ExternalUser> CreateExternalUserAsync(Domain.Entities.ExternalUser newUser, CancellationToken cancellationToken);
}
