namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Source-generated structured log messages for the review-notifications scheduled job.
/// Only user identifiers and counts are logged, never names or email addresses.
/// </summary>
internal static partial class ReviewNotificationsLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Review notification job starting")]
    public static partial void JobStarting(this ILogger logger);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "No profile reviews are due; no reminder emails sent")]
    public static partial void NoReviewsDue(this ILogger logger);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Error, Message = "Review notification job failed to read the users due a review")]
    public static partial void DueReviewLookupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "Sent review reminder to user {UserId}")]
    public static partial void ReminderSent(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Error, Message = "Failed to send review reminder to user {UserId}")]
    public static partial void ReminderFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Information, Message = "Review notification job finished. Sent: {SentCount}, failed: {FailedCount}")]
    public static partial void JobCompleted(this ILogger logger, int sentCount, int failedCount);
}
