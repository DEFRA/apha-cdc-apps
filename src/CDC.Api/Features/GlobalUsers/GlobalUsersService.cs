using System.Text.RegularExpressions;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.GlobalUsers.Commands;
using CDC.Api.Features.GlobalUsers.Dtos;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Common.Contracts;
using CDC.Graph;
using MediatR;

namespace CDC.Api.Features.GlobalUsers;

/// <summary>
/// Default <see cref="IGlobalUsersService"/>: pages the stored procedure's full result in memory
/// (it has no paging parameters) and replicates the legacy <c>GlobalUser</c> validation rules.
/// </summary>
/// <param name="repository">Global users data access.</param>
/// <param name="graphClient">Directory lookup used to pre-fill a new user's name.</param>
/// <param name="logger">Structured logger.</param>
public sealed partial class GlobalUsersService(
    IGlobalUsersRepository repository,
    IGraphClient graphClient,
    ILogger<GlobalUsersService> logger)
    : IGlobalUsersService
{
    private const int UserNameMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int RowVersionLength = 8;

    /// <summary>Matches a legacy-style domain-qualified username (e.g. <c>DOMAIN\username</c>).</summary>
    [GeneratedRegex(@"^\w+([-.]\w+)*\\[\w-]+([\. ][\w-]+)*")]
    private static partial Regex UsernameFormatRegex();

    /// <inheritdoc />
    public async Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(
        int pageNumber, int pageSize, GlobalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken)
    {
        var users = await repository.GetGlobalUsersAsync(cancellationToken);
        logger.RetrievedGlobalUsers(users.Count);

        var effectivePageNumber = Math.Max(1, pageNumber);
        var effectivePageSize = Math.Max(0, pageSize);
        var sorted = Sort(users, sortColumn, sortDescending);

        var page = effectivePageSize > 0
            ? sorted.Skip((effectivePageNumber - 1) * effectivePageSize).Take(effectivePageSize)
            : sorted;

        return new PagedResult<GlobalUserDto>
        {
            Items = [.. page.Select(ToDto)],
            PageNumber = effectivePageNumber,
            PageSize = effectivePageSize,
            TotalRecords = users.Count
        };
    }

    /// <inheritdoc />
    public async Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var result = await ClassifyAsync(userName, cancellationToken);
        logger.LookupCompleted(result.Outcome);

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SaveAsync(SaveGlobalUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await repository.GetUserAsync(command.Id, cancellationToken);

        if (!command.IsNew && existing is null)
        {
            return Result.NotFound<Unit>($"Global user '{command.Id}' was not found.");
        }

        if (existing is { IsSsoUser: true })
        {
            return Result.ValidationFailed<Unit>("SSO users cannot be given global access rights.");
        }

        // An existing user's username is never changed from here.
        var userName = existing?.UserName ?? command.UserName?.Trim() ?? string.Empty;
        var validationError = Validate(command, userName, checkUserName: existing is null);

        if (validationError is not null)
        {
            return Result.ValidationFailed<Unit>(validationError);
        }

        logger.SavingGlobalUser(command.Id);

        try
        {
            await repository.SaveGlobalUserAsync(
                new GlobalUser
                {
                    Id = command.Id,
                    UserName = userName,
                    FullName = command.FullName.Trim(),
                    Organisation = command.Organisation.Trim(),
                    SubscribedToReviewEmails = command.SubscribedToReviewEmails,
                    Role = command.Role,
                    LastUpdated = command.LastUpdated is { Length: RowVersionLength } ? command.LastUpdated : new byte[RowVersionLength]
                },
                cancellationToken);

            logger.SavedGlobalUser(command.Id);

            return Result.Success(Unit.Value);
        }
        catch (ConcurrencyException exception)
        {
            logger.GlobalUserConcurrencyConflict(command.Id);

            return Result.Conflict<Unit>(exception.Message);
        }
        catch (DuplicateUsernameException exception)
        {
            logger.GlobalUserDuplicateUsername(command.Id);

            return Result.ValidationFailed<Unit>(exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteAsync(DeleteGlobalUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.RevokingGlobalUser(command.Id);

        try
        {
            await repository.DeleteGlobalUserAsync(command.Id, command.LastUpdated, cancellationToken);

            logger.RevokedGlobalUser(command.Id);

            return Result.Success(Unit.Value);
        }
        catch (ConcurrencyException exception)
        {
            logger.GlobalUserConcurrencyConflict(command.Id);

            return Result.Conflict<Unit>(exception.Message);
        }
    }

    private async Task<GlobalUserLookupResult> ClassifyAsync(string userName, CancellationToken cancellationToken)
    {
        var match = await repository.FindUserByUsernameAsync(userName, cancellationToken);

        if (match is not null)
        {
            if (match.IsUserManagementSystem)
            {
                return new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.Blocked };
            }

            var user = await repository.GetUserAsync(match.Id, cancellationToken);

            if (user is not null)
            {
                return user.IsSsoUser
                    ? new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.SsoUser }
                    : new GlobalUserLookupResult
                    {
                        Outcome = GlobalUserLookupOutcome.ExistingUser,
                        Id = user.Id,
                        UserName = user.UserName,
                        FullName = user.FullName,
                        Organisation = user.Organisation,
                        Role = user.Role,
                        SubscribedToReviewEmails = user.SubscribedToReviewEmails,
                        LastUpdated = user.LastUpdated
                    };
            }
        }

        if (!UsernameFormatRegex().IsMatch(userName))
        {
            return new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.InvalidFormat };
        }

        var directoryUser = await FindInDirectoryAsync(userName, cancellationToken);

        return new GlobalUserLookupResult
        {
            Outcome = GlobalUserLookupOutcome.NewUser,
            Id = Guid.NewGuid(),
            UserName = userName,
            FullName = Truncate(directoryUser?.DisplayName),
            Organisation = string.Empty,
            LastUpdated = new byte[RowVersionLength]
        };
    }

    /// <summary>Looks a not-yet-known user up in the directory. A failure is logged and treated as "not found" so adding a user by hand still works.</summary>
    private async Task<GraphUser?> FindInDirectoryAsync(string userName, CancellationToken cancellationToken)
    {
        // The directory matches on the logon name alone; the legacy DOMAIN\ prefix is not a Graph property.
        var samAccountName = userName[(userName.IndexOf('\\', StringComparison.Ordinal) + 1)..];

        try
        {
            var user = await graphClient.FindUserBySamAccountNameAsync(samAccountName, cancellationToken);
            logger.GraphLookupCompleted(user is not null);

            return user;
        }
        catch (GraphException exception)
        {
            logger.GraphLookupFailed(exception);

            return null;
        }
    }

    // Keeps a directory value within the length the save step accepts.
    private static string Truncate(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        return trimmed.Length > NameMaxLength ? trimmed[..NameMaxLength] : trimmed;
    }

    /// <summary>Returns the first validation failure message, or <see langword="null"/> when the change is valid.</summary>
    private static string? Validate(SaveGlobalUserCommand command, string userName, bool checkUserName)
    {
        // Stored usernames (e.g. Entra-style) need not match the legacy Domain\UserId format.
        if (checkUserName && (string.IsNullOrWhiteSpace(userName) || userName.Length > UserNameMaxLength || !UsernameFormatRegex().IsMatch(userName)))
        {
            return "Enter a username in the format Domain\\UserId.";
        }

        if (command.Role is not (GlobalUserRole.ProfileEditor or GlobalUserRole.PolicyProfileUser))
        {
            return "Select a valid role.";
        }

        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            return "Enter a full name.";
        }

        if (command.FullName.Trim().Length > NameMaxLength)
        {
            return $"Full name must be {NameMaxLength} characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(command.Organisation))
        {
            return "Enter an organisation.";
        }

        if (command.Organisation.Trim().Length > NameMaxLength)
        {
            return $"Organisation must be {NameMaxLength} characters or fewer.";
        }

        return null;
    }

    private static IEnumerable<GlobalUser> Sort(IEnumerable<GlobalUser> users, GlobalUserSortColumn column, bool descending)
    {
        Func<GlobalUser, string> key = column switch
        {
            GlobalUserSortColumn.UserName => user => user.UserName,
            GlobalUserSortColumn.Organisation => user => user.Organisation,
            // Legacy sorted on the role's name, not its numeric value.
            GlobalUserSortColumn.Role => user => user.Role.ToString(),
            _ => user => user.FullName
        };

        return descending
            ? users.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
            : users.OrderBy(key, StringComparer.OrdinalIgnoreCase);
    }

    private static GlobalUserDto ToDto(GlobalUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        FullName = user.FullName,
        Organisation = user.Organisation,
        Role = user.Role,
        SubscribedToReviewEmails = user.SubscribedToReviewEmails,
        LastUpdated = user.LastUpdated
    };
}
