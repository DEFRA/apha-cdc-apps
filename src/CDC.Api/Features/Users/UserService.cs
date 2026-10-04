using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;

namespace CDC.Api.Features.Users;

/// <summary>
/// Default <see cref="IUserService"/>: CidmSsoId-then-email resolution, the external/not-external
/// decision, and new-user provisioning, on top of the plain CRUD <see cref="IUserRepository"/>.
/// </summary>
/// <param name="userRepository">User data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UserService(IUserRepository userRepository, ILogger<UserService> logger) : IUserService
{
    /// <inheritdoc />
    public async Task<Result<ExternalUser>> ResolveExternalUserAsync(
        ResolveExternalUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var bySsoId = await userRepository.GetByCidmSsoIdAsync(command.CidmSsoId, cancellationToken);
        if (bySsoId is not null)
        {
            logger.MatchedByCidmSsoId(bySsoId.Id);
            return Result.Success(bySsoId);
        }

        var byEmail = await userRepository.GetByEmailAddressAsync(command.Email, cancellationToken);
        if (byEmail is not null)
        {
            return await LinkOrDenyAsync(byEmail, command.CidmSsoId, cancellationToken);
        }

        return Result.Success(await CreateAsync(command, cancellationToken));
    }

    private async Task<Result<ExternalUser>> LinkOrDenyAsync(
        ExternalUser existingUser,
        Guid cidmSsoId,
        CancellationToken cancellationToken)
    {
        // SsoUserId is the pre-existing (legacy, pre-CIDM) signal that a row is an external user -
        // a row found only by email with no SsoUserId belongs to an internal-only account, and
        // must not be linked to an external CIDM identity.
        if (!existingUser.SsoUserId.HasValue)
        {
            logger.EmailMatchedNonExternalUser(existingUser.Id);
            return Result.Forbidden<ExternalUser>(
                $"The email address '{existingUser.EmailAddress}' belongs to an existing account that is not permitted to sign in externally.");
        }

        logger.MatchedByEmailBackfillingCidmSsoId(existingUser.Id);
        await userRepository.UpdateCidmSsoIdAsync(existingUser.Id, cidmSsoId, cancellationToken);

        return Result.Success(existingUser with { CidmSsoId = cidmSsoId });
    }

    private async Task<ExternalUser> CreateAsync(ResolveExternalUserCommand command, CancellationToken cancellationToken)
    {
        // UserName is set to the email address for new records, per the agreed convention; every
        // other column is only ever populated from CIDM claims here, at creation time - later
        // sign-ins never overwrite them again.
        var newUser = new ExternalUser
        {
            Id = Guid.NewGuid(),
            UserName = command.Email,
            FullName = $"{command.FirstName} {command.LastName}".Trim(),
            Organisation = command.Organisation,
            EmailAddress = command.Email,
            CidmSsoId = command.CidmSsoId,
            SsoUserId = null,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        var created = await userRepository.CreateExternalUserAsync(newUser, cancellationToken);
        logger.CreatedNewExternalUser(created.Id);

        return created;
    }
}
