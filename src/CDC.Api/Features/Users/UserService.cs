using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;

namespace CDC.Api.Features.Users;

/// <summary>
/// Default <see cref="IUserService"/>: SsoUserIdExt-then-email resolution, the external/not-external
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

        var bySsoId = await userRepository.GetBySsoUserIdExtAsync(command.SsoUserIdExt, cancellationToken);
        if (bySsoId is not null)
        {
            logger.MatchedBySsoUserIdExt(bySsoId.Id);
            return Result.Success(bySsoId);
        }

        var byEmail = await userRepository.GetByEmailAddressAsync(command.Email, cancellationToken);
        if (byEmail is not null)
        {
            return await LinkOrDenyAsync(byEmail, command.SsoUserIdExt, cancellationToken);
        }

        return Result.Success(await CreateAsync(command, cancellationToken));
    }

    private async Task<Result<ExternalUser>> LinkOrDenyAsync(
        ExternalUser existingUser,
        Guid ssoUserIdExt,
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

        logger.MatchedByEmailBackfillingSsoUserIdExt(existingUser.Id);
        await userRepository.UpdateSsoUserIdExtAsync(existingUser.Id, ssoUserIdExt, cancellationToken);

        return Result.Success(existingUser with { SsoUserIdExt = ssoUserIdExt });
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
            SsoUserIdExt = command.SsoUserIdExt,
            SsoUserId = null,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        var created = await userRepository.CreateExternalUserAsync(newUser, cancellationToken);
        logger.CreatedNewExternalUser(created.Id);

        return created;
    }

    /// <inheritdoc />
    public async Task<Result<InternalUser>> ResolveInternalUserAsync(
        ResolveInternalUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var bySsoId = await userRepository.GetBySsoUserIdIntAsync(command.SsoUserIdInt, cancellationToken);
        if (bySsoId is not null)
        {
            logger.MatchedBySsoUserIdInt(bySsoId.Id);
            return Result.Success(bySsoId);
        }

        var byUserName = await userRepository.GetByUserNameAsync(command.UserName, cancellationToken);
        if (byUserName is null)
        {
            // Legacy parity: a Windows/Entra-authenticated user with no [dbo].[User] row is still
            // let in - just with no profile-authoring privileges (IsProfileEditor/IsPolicyProfileUser
            // both false, no linked Id) - rather than being denied sign-in outright.
            logger.GrantedLimitedAccessForUnmatchedInternalUser();
            return Result.Success(new InternalUser
            {
                Id = Guid.Empty,
                UserName = command.UserName,
                FullName = command.FullName,
                SsoUserIdInt = null,
                IsProfileEditor = false,
                IsPolicyProfileUser = false
            });
        }

        logger.MatchedByUserNameBackfillingSsoUserIdInt(byUserName.Id);
        await userRepository.UpdateSsoUserIdIntAsync(byUserName.Id, command.SsoUserIdInt, cancellationToken);

        return Result.Success(byUserName with { SsoUserIdInt = command.SsoUserIdInt });
    }
}
