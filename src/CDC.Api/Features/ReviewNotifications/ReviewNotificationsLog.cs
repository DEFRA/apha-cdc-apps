namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Source-generated structured log messages for the review-notifications scheduled job.
/// Nothing here records personal data: only a database timestamp is logged.
/// </summary>
internal static partial class ReviewNotificationsLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Review notification job scaffold starting")]
    public static partial void JobStarting(this ILogger logger);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "Review notification job scaffold confirmed database connectivity. Database time (UTC): {DatabaseTimeUtc}")]
    public static partial void DatabaseConnectivityConfirmed(this ILogger logger, DateTime databaseTimeUtc);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Error, Message = "Review notification job scaffold failed to connect to the database")]
    public static partial void DatabaseConnectivityFailed(this ILogger logger, Exception exception);
}
