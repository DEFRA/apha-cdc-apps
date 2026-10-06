namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Source-generated structured log messages for the review-notifications scheduled job.
/// Nothing here records personal data: only counts are logged, never a recipient's name or
/// email address.
/// </summary>
internal static partial class ReviewNotificationsLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Review notification job starting")]
    public static partial void JobStarting(this ILogger logger);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "Review notification job resolved {RecipientCount} subscribed recipients due a review email")]
    public static partial void ResolvedRecipients(this ILogger logger, int recipientCount);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Error, Message = "Review notification job failed to resolve its recipients")]
    public static partial void RecipientQueryFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Error, Message = "Review notification job failed to connect to the database")]
    public static partial void DatabaseConnectivityFailed(this ILogger logger, Exception exception);
}
