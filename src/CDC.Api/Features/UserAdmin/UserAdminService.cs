using CDC.Api.Features.UserAdmin.Commands;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Interfaces;
using CDC.Api.Features.UserAdmin.Mapping;

namespace CDC.Api.Features.UserAdmin;

/// <summary>
/// Default <see cref="IUserAdminService"/>: reads through <see cref="IUserAdminRepository"/>
/// and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">User administration data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UserAdminService(IUserAdminRepository repository, ILogger<UserAdminService> logger) : IUserAdminService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainedUserDto>> GetGlobalUsersAsync(CancellationToken cancellationToken)
    {
        var users = await repository.GetGlobalUsersAsync(cancellationToken);
        logger.RetrievedGlobalUsers(users.Count);

        return [.. users.Select(user => user.ToDto())];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainedUserDto>> GetExternalUsersAsync(CancellationToken cancellationToken)
    {
        var users = await repository.GetExternalUsersAsync(cancellationToken);
        logger.RetrievedExternalUsers(users.Count);

        return [.. users.Select(user => user.ToDto())];
    }

    /// <inheritdoc />
    public async Task<MaintainedUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserAsync(userId, cancellationToken);

        if (user is null)
        {
            logger.UserNotFound(userId);
            return null;
        }

        return user.ToDto();
    }

    /// <inheritdoc />
    public async Task<UpdateReviewEmailSubscriptionResultDto?> UpdateReviewEmailSubscriptionAsync(
        UpdateReviewEmailSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var lastUpdated = await repository.UpdateReviewEmailSubscriptionAsync(
            command.UserId,
            command.SubscribedToReviewEmails,
            command.LastUpdated,
            cancellationToken);

        if (lastUpdated is null)
        {
            logger.UserNotFound(command.UserId);
            return null;
        }

        logger.UpdatedReviewEmailSubscription(command.UserId, command.SubscribedToReviewEmails);

        return new UpdateReviewEmailSubscriptionResultDto
        {
            UserId = command.UserId,
            SubscribedToReviewEmails = command.SubscribedToReviewEmails,
            LastUpdated = lastUpdated
        };
    }
}
