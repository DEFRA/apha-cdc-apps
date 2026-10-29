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

    [LoggerMessage(EventId = 7006, Level = LogLevel.Error, Message = "No reviewer group is configured for {ReviewerGroup}; reviews of that type were not emailed")]
    public static partial void ReviewerGroupNotConfigured(this ILogger logger, string reviewerGroup);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Information, Message = "Review notification job finished. Sent: {SentCount}, failed: {FailedCount}")]
    public static partial void JobCompleted(this ILogger logger, int sentCount, int failedCount);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Critical, Message = "Review notification job terminated unexpectedly")]
    public static partial void JobTerminated(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Error, Message = "Failed to build the comparison report for user {UserId}; the reminder is sent without it")]
    public static partial void ComparisonReportFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Warning, Message = "The comparison report for user {UserId} is {SizeBytes} bytes, over the Notify limit; the reminder is sent without it")]
    public static partial void ComparisonReportTooLarge(this ILogger logger, Guid userId, int sizeBytes);

    [LoggerMessage(EventId = 7010, Level = LogLevel.Information, Message = "No comparison report attached: Notify:AttachmentTemplateId is not set")]
    public static partial void AttachmentsNotConfigured(this ILogger logger);

    [LoggerMessage(EventId = 7011, Level = LogLevel.Information, Message = "No comparison for draft version {ProfileVersionId}: {Reason}")]
    public static partial void ComparisonSkipped(this ILogger logger, Guid profileVersionId, string reason);

    [LoggerMessage(EventId = 7012, Level = LogLevel.Information, Message = "Attaching a comparison report of {ComparisonCount} profile(s) for user {UserId}")]
    public static partial void ComparisonAttached(this ILogger logger, int comparisonCount, Guid userId);
}
