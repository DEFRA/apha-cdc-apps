using System.Data.Common;
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
    // [dbo].[User].EmailAddress is varchar(50).
    private const int EmailAddressMaxLength = 50;

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

        // Self-registration is blocked: a profile editor must pre-register the user first.
        logger.ExternalUserNotPreRegistered(command.SsoUserIdExt);
        return Result.Forbidden<ExternalUser>(
            $"The email address '{command.Email}' has not been pre-registered for external access.");
    }

    private async Task<Result<ExternalUser>> LinkOrDenyAsync(
        ExternalUser existingUser,
        Guid ssoUserIdExt,
        CancellationToken cancellationToken)
    {
        // SsoUserId is the pre-existing (legacy, pre-CIDM) signal that a row is an external user -
        // a row found only by email with no SsoUserId belongs to an internal-only account, and
        // must not be linked to an external CIDM identity. The one exception is an internal profile
        // editor, who signs in through CIDM to manage external users' organisation accounts: they are
        // let in without a CIDM id being stored, so the row stays an internal user.
        if (!existingUser.SsoUserId.HasValue)
        {
            if (existingUser.SsoUserIdInt.HasValue && existingUser.IsProfileEditor)
            {
                logger.InternalProfileEditorSignedInThroughCidm(existingUser.Id);
                return Result.Success(existingUser);
            }

            logger.EmailMatchedNonExternalUser(existingUser.Id);
            return Result.Forbidden<ExternalUser>(
                $"The email address '{existingUser.EmailAddress}' belongs to an existing account that is not permitted to sign in externally.");
        }

        logger.MatchedByEmailBackfillingSsoUserIdExt(existingUser.Id);
        await userRepository.UpdateSsoUserIdExtAsync(existingUser.Id, ssoUserIdExt, cancellationToken);

        return Result.Success(existingUser with { SsoUserIdExt = ssoUserIdExt });
    }

    /// <inheritdoc />
    public async Task<Result<InternalUser>> ResolveInternalUserAsync(
        ResolveInternalUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await FindOrCreateInternalUserAsync(command, cancellationToken);
        await RecordEmailAddressAsync(user, command.Email, cancellationToken);

        return Result.Success(user);
    }

    private async Task<InternalUser> FindOrCreateInternalUserAsync(ResolveInternalUserCommand command, CancellationToken cancellationToken)
    {
        var bySsoId = await userRepository.GetBySsoUserIdIntAsync(command.SsoUserIdInt, cancellationToken);
        if (bySsoId is not null)
        {
            logger.MatchedBySsoUserIdInt(bySsoId.Id);
            return bySsoId;
        }

        var byUserName = await userRepository.GetByUserNameAsync(command.UserName, cancellationToken);
        if (byUserName is null)
        {
            return await CreateInternalUserAsync(command, cancellationToken);
        }

        logger.MatchedByUserNameBackfillingSsoUserIdInt(byUserName.Id);
        await userRepository.UpdateSsoUserIdIntAsync(byUserName.Id, command.SsoUserIdInt, cancellationToken);

        return byUserName with { SsoUserIdInt = command.SsoUserIdInt };
    }

    // CIDM sign-in finds a user by email address, and an internal row has none until it is recorded here. Only a missing
    // address is filled in, and a failure must never stop the user signing in.
    private async Task RecordEmailAddressAsync(InternalUser user, string? email, CancellationToken cancellationToken)
    {
        var emailAddress = email?.Trim();
        if (string.IsNullOrEmpty(emailAddress) || emailAddress.Length > EmailAddressMaxLength || !string.IsNullOrWhiteSpace(user.EmailAddress))
        {
            return;
        }

        try
        {
            if (await userRepository.UpdateEmailAddressAsync(user.Id, emailAddress, cancellationToken))
            {
                logger.RecordedInternalUserEmailAddress(user.Id);
            }
        }
        catch (DbException exception)
        {
            logger.FailedToRecordInternalUserEmailAddress(exception, user.Id);
        }
    }

    private async Task<InternalUser> CreateInternalUserAsync(ResolveInternalUserCommand command, CancellationToken cancellationToken)
    {
        // No privileges: a profile editor grants a role later, on the Global users page.
        var newUser = new InternalUser
        {
            Id = Guid.NewGuid(),
            UserName = command.UserName,
            FullName = string.IsNullOrWhiteSpace(command.FullName) ? command.UserName : command.FullName.Trim(),
            SsoUserIdInt = command.SsoUserIdInt,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        var created = await userRepository.CreateInternalUserAsync(newUser, cancellationToken);
        logger.CreatedNewInternalUser(created.Id);

        return created;
    }
}
