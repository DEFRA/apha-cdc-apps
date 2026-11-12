using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Dtos;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ExternalUsers;

/// <summary>Default <see cref="IExternalUsersService"/>; pages the stored procedure's full result in memory.</summary>
/// <param name="repository">External users data access.</param>
/// <param name="contributorsService">Writes role and section permissions, so external users follow the same rules as other contributors.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ExternalUsersService(
    IExternalUsersRepository repository,
    IProfileContributorsService contributorsService,
    ILogger<ExternalUsersService> logger)
    : IExternalUsersService
{
    /// <inheritdoc />
    public async Task<PagedResult<ExternalUserDto>> GetExternalUsersAsync(
        int pageNumber, int pageSize, ExternalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken)
    {
        var users = await repository.GetExternalUsersAsync(cancellationToken);
        var awaitingInstructions = await repository.GetUsersAwaitingLoginInstructionsAsync(cancellationToken);
        logger.RetrievedExternalUsers(users.Count);

        var effectivePageNumber = Math.Max(1, pageNumber);
        var effectivePageSize = Math.Max(0, pageSize);
        var sorted = Sort(users, sortColumn, sortDescending);

        var page = effectivePageSize > 0
            ? sorted.Skip((effectivePageNumber - 1) * effectivePageSize).Take(effectivePageSize)
            : sorted;

        return new PagedResult<ExternalUserDto>
        {
            Items = [.. page.Select(user => ToDto(user, !awaitingInstructions.Contains(user.Id)))],
            PageNumber = effectivePageNumber,
            PageSize = effectivePageSize,
            TotalRecords = users.Count
        };
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SetReviewEmailSubscriptionAsync(SetExternalUserSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var users = await repository.GetExternalUsersAsync(cancellationToken);
        var existing = users.FirstOrDefault(user => user.Id == command.Id);

        if (existing is null)
        {
            return Result.NotFound<Unit>($"External user '{command.Id}' was not found.");
        }

        await repository.SaveExternalUserAsync(existing with { SubscribedToReviewEmails = command.SubscribedToReviewEmails }, cancellationToken);
        logger.ReviewEmailSubscriptionChanged(command.Id, command.SubscribedToReviewEmails);

        return Result.Success(Unit.Value);
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> PreRegisterExternalUserAsync(PreRegisterExternalUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var emailAddress = command.EmailAddress.Trim();
        var id = Guid.NewGuid();

        // UserName is the email address, per the convention used for CIDM-provisioned users.
        var created = await repository.PreRegisterExternalUserAsync(
            id, emailAddress, command.FullName.Trim(), command.Organisation.Trim(), emailAddress, cancellationToken);

        if (!created)
        {
            return Result.Conflict<Unit>($"A user with the email address '{emailAddress}' already exists.");
        }

        logger.ExternalUserPreRegistered(id);

        return Result.Success(Unit.Value);
    }

    /// <inheritdoc />
    public async Task<Result<ExternalUserProfileAccessContract>> GetProfileAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsExternalUserAsync(userId, cancellationToken))
        {
            return Result.NotFound<ExternalUserProfileAccessContract>($"External user '{userId}' was not found.");
        }

        var access = await repository.GetProfileAccessAsync(userId, cancellationToken);

        return Result.Success(new ExternalUserProfileAccessContract
        {
            Profiles = [.. access.Profiles.Select(grant => new ExternalUserProfileGrantContract
            {
                ProfileId = grant.ProfileId,
                Title = grant.Title,
                RoleId = grant.RoleId,
                RoleName = grant.RoleName,
                IsContributor = grant.IsContributor,
                SectionPermissionIds = grant.SectionPermissionIds
            })],
            AvailableProfiles = [.. access.AvailableProfiles.Select(profile => new ProfileOptionContract { Id = profile.Id, Title = profile.Title })]
        });
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SaveProfileAccessAsync(SaveExternalUserProfileAccessCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await IsExternalUserAsync(command.UserId, cancellationToken))
        {
            return Result.NotFound<Unit>($"External user '{command.UserId}' was not found.");
        }

        var existing = await contributorsService.GetContributorForEditAsync(command.ProfileId, command.UserId, cancellationToken);

        if (existing is null)
        {
            return Result.NotFound<Unit>($"External user '{command.UserId}' was not found.");
        }

        // spgContributor reports no role when the user is not yet on this profile.
        var result = existing.RoleId == Guid.Empty
            ? await contributorsService.AddContributorAsync(
                new AddContributorCommand(
                    command.ProfileId, command.UserId, existing.UserName, IsSsoUser: true, command.RoleId,
                    existing.FullName, existing.Organisation, command.SectionPermissionIds),
                cancellationToken)
            : await contributorsService.UpdateContributorAsync(
                new UpdateContributorCommand(
                    command.ProfileId, command.UserId, command.RoleId,
                    existing.FullName, existing.Organisation, command.SectionPermissionIds, existing.LastUpdated),
                cancellationToken);

        if (result.Status == ResultStatus.Success)
        {
            logger.ProfileAccessSaved(command.UserId, command.ProfileId);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteProfileAccessAsync(DeleteExternalUserProfileAccessCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await IsExternalUserAsync(command.UserId, cancellationToken))
        {
            return Result.NotFound<Unit>($"External user '{command.UserId}' was not found.");
        }

        var existing = await contributorsService.GetContributorForEditAsync(command.ProfileId, command.UserId, cancellationToken);

        if (existing is null || existing.RoleId == Guid.Empty)
        {
            return Result.NotFound<Unit>($"External user '{command.UserId}' does not have access to profile '{command.ProfileId}'.");
        }

        var result = await contributorsService.DeleteContributorAsync(
            new DeleteContributorCommand(command.ProfileId, command.UserId, existing.LastUpdated), cancellationToken);

        if (result.Status == ResultStatus.Success)
        {
            logger.ProfileAccessRemoved(command.UserId, command.ProfileId);
        }

        return result;
    }

    private async Task<bool> IsExternalUserAsync(Guid userId, CancellationToken cancellationToken) =>
        (await repository.GetExternalUsersAsync(cancellationToken)).Any(user => user.Id == userId);

    private static IEnumerable<ExternalUserAccount> Sort(IEnumerable<ExternalUserAccount> users, ExternalUserSortColumn column, bool descending)
    {
        Func<ExternalUserAccount, string>? key = column switch
        {
            ExternalUserSortColumn.UserName => user => user.UserName,
            ExternalUserSortColumn.FullName => user => user.FullName,
            ExternalUserSortColumn.EmailAddress => user => user.EmailAddress,
            ExternalUserSortColumn.Organisation => user => user.Organisation,
            _ => null
        };

        if (key is null)
        {
            return users;
        }

        return descending
            ? users.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
            : users.OrderBy(key, StringComparer.OrdinalIgnoreCase);
    }

    private static ExternalUserDto ToDto(ExternalUserAccount user, bool loginInstructionsSent) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        FullName = user.FullName,
        EmailAddress = user.EmailAddress,
        Organisation = user.Organisation,
        SubscribedToReviewEmails = user.SubscribedToReviewEmails,
        LoginInstructionsSent = loginInstructionsSent
    };
}
