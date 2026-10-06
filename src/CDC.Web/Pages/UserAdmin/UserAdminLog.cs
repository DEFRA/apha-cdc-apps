namespace CDC.Web.Pages.UserAdmin;

/// <summary>
/// Source-generated structured log messages for the user administration pages. Nothing here
/// records personal data: only identifiers and outcomes are logged.
/// </summary>
internal static partial class UserAdminLog
{
    [LoggerMessage(EventId = 2200, Level = LogLevel.Error, Message = "Failed to load user accounts from CDC.Api")]
    public static partial void FailedToLoadUsers(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Error, Message = "Failed to load user {UserId} from CDC.Api")]
    public static partial void FailedToLoadUser(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Warning,
        Message = "Failed to save the review email subscription for user {UserId}: {Outcome}")]
    public static partial void FailedToSaveSubscription(this ILogger logger, Guid userId, Models.ReviewEmailSubscriptionOutcome outcome);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Information,
        Message = "Saved the review email subscription for user {UserId}")]
    public static partial void SavedSubscription(this ILogger logger, Guid userId);
}
