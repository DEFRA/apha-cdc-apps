namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Source-generated structured log messages for the review-notifications scheduled job.
/// Only user ids and counts are logged - never names, email addresses or email content.
/// </summary>
internal static partial class ReviewNotificationsLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Review notification job starting")]
    public static partial void JobStarting(this ILogger logger);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Warning,
        Message = "Review notification job is disabled (SmtpSettings:Enabled is false); no emails were sent")]
    public static partial void JobDisabled(this ILogger logger);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information, Message = "Found {RecipientCount} user(s) due a review reminder")]
    public static partial void RecipientsFound(this ILogger logger, int recipientCount);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "Review reminder sent for user {UserId}")]
    public static partial void EmailSent(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Error, Message = "Review reminder failed for user {UserId}")]
    public static partial void EmailFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Information,
        Message = "Review notification job finished: {SentCount} sent, {FailedCount} failed")]
    public static partial void JobFinished(this ILogger logger, int sentCount, int failedCount);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Error, Message = "Review notification job could not read the users due a review")]
    public static partial void RecipientsReadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Error, Message = "Review notification job mail settings are invalid")]
    public static partial void ConfigurationInvalid(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Error, Message = "Review notification job stopped unexpectedly")]
    public static partial void JobFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Error, Message = "Database call {Operation} failed")]
    public static partial void DatabaseCallFailed(this ILogger logger, Exception exception, string operation);

    [LoggerMessage(EventId = 7010, Level = LogLevel.Warning,
        Message = "Microsoft Graph returned {StatusCode}; retrying in {DelaySeconds}s (attempt {Attempt})")]
    public static partial void GraphRetrying(this ILogger logger, int statusCode, double delaySeconds, int attempt);
}
