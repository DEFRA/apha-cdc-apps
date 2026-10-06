namespace CDC.Api.Features.UserAdmin;

/// <summary>
/// Source-generated structured log messages for the user administration feature. Nothing here
/// records personal data: only identifiers, counts and the subscription flag are logged.
/// </summary>
internal static partial class UserAdminLog
{
    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "Retrieved {UserCount} global users")]
    public static partial void RetrievedGlobalUsers(this ILogger logger, int userCount);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Retrieved {UserCount} external users")]
    public static partial void RetrievedExternalUsers(this ILogger logger, int userCount);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Warning, Message = "User {UserId} was not found")]
    public static partial void UserNotFound(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information,
        Message = "Set the review email subscription for user {UserId} to {Subscribed}")]
    public static partial void UpdatedReviewEmailSubscription(this ILogger logger, Guid userId, bool subscribed);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Warning,
        Message = "Rejected a concurrent change to the review email subscription for user {UserId}")]
    public static partial void SubscriptionConcurrencyConflict(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
