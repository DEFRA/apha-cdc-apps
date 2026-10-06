using CDC.Api.Features.ReviewNotifications.Interfaces;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Entry point for the review-due notification scheduled job (an Amazon EventBridge
/// Scheduler-triggered ECS Scheduled Task reusing this image with a "review-notifications"
/// command override, per the D2R2 LLD Scheduling section - no separate Lambda or image).
/// It resolves the recipients who are due a review notification; the GOV.UK Notify send step
/// will be added here once the notification service is built.
/// </summary>
/// <param name="repository">Review notification data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReviewNotificationsJob(IReviewNotificationRepository repository, ILogger<ReviewNotificationsJob> logger)
{
    /// <summary>Runs the job. Returns true on success, false on failure.</summary>
    /// <param name="cancellationToken">Cancels the recipient query.</param>
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        logger.JobStarting();

        try
        {
            // Unsubscribed users are filtered out by the query itself, so they are never
            // notified regardless of what review activity has taken place.
            var recipients = await repository.GetRecipientsDueReviewEmailAsync(cancellationToken);

            logger.ResolvedRecipients(recipients.Count);

            return true;
        }
        catch (Exception ex)
        {
            logger.RecipientQueryFailed(ex);
            return false;
        }
    }
}
