using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Scheduled job that emails each subscribed user a reminder of the draft profile reviews that are
/// overdue or due within two weeks. An Amazon EventBridge Scheduler-triggered ECS Scheduled Task
/// reuses the API image with a "review-notifications" command override. Failures are logged and
/// reflected in the exit code so a failed run is visible to support.
/// </summary>
public sealed class ReviewNotificationsJob(
    IReviewNotificationRepository repository,
    IReviewEmailSender sender,
    IOptions<GraphMailOptions> mailOptions,
    IOptions<ReviewNotificationsOptions> contentOptions,
    TimeProvider timeProvider,
    ILogger<ReviewNotificationsJob> logger)
{
    /// <summary>Runs the job. Returns true when every email was sent, false if anything failed.</summary>
    /// <param name="cancellationToken">Cancels the job.</param>
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        logger.JobStarting();

        try
        {
            if (!mailOptions.Value.Enabled)
            {
                logger.JobDisabled();
                return true;
            }

            if (!TryValidateConfiguration())
            {
                return false;
            }

            var recipients = await ReadRecipientsAsync(cancellationToken);
            if (recipients is null)
            {
                return false;
            }

            var withReviews = recipients.Where(r => r.ProfileVersions.Any(v => v.Sections.Count > 0)).ToList();
            logger.RecipientsFound(withReviews.Count);

            var today = timeProvider.GetUtcNow().UtcDateTime.Date;
            var sent = 0;
            var failed = 0;

            foreach (var recipient in withReviews)
            {
                try
                {
                    await sender.SendAsync(ReviewEmailBuilder.Build(recipient, contentOptions.Value, today), cancellationToken);
                    sent++;
                    logger.EmailSent(recipient.UserId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    logger.EmailFailed(ex, recipient.UserId);
                }
            }

            logger.JobFinished(sent, failed);
            return failed == 0;
        }
        catch (Exception ex)
        {
            logger.JobFailed(ex);
            return false;
        }
    }

    private bool TryValidateConfiguration()
    {
        try
        {
            sender.ValidateConfiguration();
            return true;
        }
        catch (InvalidOperationException ex)
        {
            logger.ConfigurationInvalid(ex);
            return false;
        }
    }

    private async Task<IReadOnlyList<ReviewNotificationRecipient>?> ReadRecipientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await repository.GetRecipientsDueReviewAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RecipientsReadFailed(ex);
            return null;
        }
    }
}
