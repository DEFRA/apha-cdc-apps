using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>
/// Data access for the review-due notification scheduled job.
/// </summary>
public interface IReviewNotificationRepository
{
    /// <summary>
    /// Reads the users who are due a review notification email. Users who are unsubscribed from
    /// review emails are excluded, so an unsubscribed user is never a recipient.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The subscribed users who are due a notification.</returns>
    Task<IReadOnlyList<ReviewEmailRecipient>> GetRecipientsDueReviewEmailAsync(CancellationToken cancellationToken);
}
