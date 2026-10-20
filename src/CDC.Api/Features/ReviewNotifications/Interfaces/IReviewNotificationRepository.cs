using CDC.Api.Features.ReviewNotifications.Dtos;

namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>Reads the users who are due a review reminder.</summary>
public interface IReviewNotificationRepository
{
    /// <summary>Gets subscribed users with draft profile sections that are overdue or due within two weeks.</summary>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    Task<IReadOnlyList<ReviewNotificationRecipient>> GetRecipientsDueReviewAsync(CancellationToken cancellationToken);
}
