using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Microsoft.Extensions.Options;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Entry point for the review-due notification scheduled job (an Amazon EventBridge
/// Scheduler-triggered ECS Scheduled Task reusing this image with a "review-notifications"
/// command override). Sends each subscribed user one reminder listing their profile sections
/// due for review; sends nothing when no review is due.
/// </summary>
public sealed class ReviewNotificationsJob(
    IReviewNotificationsRepository repository,
    IEmailSender emailSender,
    IOptions<ReviewNotificationsOptions> options,
    TimeProvider timeProvider,
    ILogger<ReviewNotificationsJob> logger)
{
    /// <summary>Runs the job. Returns true when every due reminder was sent, false if any failure occurred.</summary>
    /// <param name="cancellationToken">Cancels the job.</param>
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        logger.JobStarting();

        IReadOnlyList<ReviewDueUser> users;
        try
        {
            users = await repository.GetUsersDueReviewAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.DueReviewLookupFailed(ex);
            return false;
        }

        if (users.Count == 0)
        {
            logger.NoReviewsDue();
            return true;
        }

        var today = timeProvider.GetLocalNow().DateTime;
        var failed = 0;

        foreach (var user in users)
        {
            try
            {
                await emailSender.SendAsync(ReviewEmailComposer.Compose(user, today, options.Value), cancellationToken);
                logger.ReminderSent(user.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.ReminderFailed(ex, user.Id);
            }
        }

        logger.JobCompleted(users.Count - failed, failed);

        return failed == 0;
    }
}
