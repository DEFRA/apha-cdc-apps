using CDC.Api.Features.ReviewNotifications.Dtos;

namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>Reads the users and profile sections that are due a review reminder.</summary>
public interface IReviewNotificationsRepository
{
    /// <summary>Gets every subscribed user with a draft profile section due for review within the reminder window.</summary>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    Task<IReadOnlyList<ReviewDueUser>> GetUsersDueReviewAsync(CancellationToken cancellationToken);
}
