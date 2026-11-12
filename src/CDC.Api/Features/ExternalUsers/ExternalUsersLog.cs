namespace CDC.Api.Features.ExternalUsers;

/// <summary>Source-generated structured log messages for the external users feature. Only identifiers and counts are logged.</summary>
internal static partial class ExternalUsersLog
{
    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "Retrieved {UserCount} external users")]
    public static partial void RetrievedExternalUsers(this ILogger logger, int userCount);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Information, Message = "Set review email subscription for external user {UserId} to {Subscribed}")]
    public static partial void ReviewEmailSubscriptionChanged(this ILogger logger, Guid userId, bool subscribed);

    [LoggerMessage(EventId = 1505, Level = LogLevel.Information, Message = "Pre-registered external user {UserId}")]
    public static partial void ExternalUserPreRegistered(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Information, Message = "Saved profile access for external user {UserId} on profile {ProfileId}")]
    public static partial void ProfileAccessSaved(this ILogger logger, Guid userId, Guid profileId);

    [LoggerMessage(EventId = 1504, Level = LogLevel.Information, Message = "Removed profile access for external user {UserId} on profile {ProfileId}")]
    public static partial void ProfileAccessRemoved(this ILogger logger, Guid userId, Guid profileId);

    [LoggerMessage(EventId = 1506, Level = LogLevel.Information, Message = "Sent login instructions to external user {UserId}")]
    public static partial void LoginInstructionsSent(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1507, Level = LogLevel.Warning, Message = "Login instructions not sent: external user {UserId} has no email address")]
    public static partial void LoginInstructionsNoEmailAddress(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1508, Level = LogLevel.Error, Message = "Login instructions email could not be sent to external user {UserId}; the user stays unsent")]
    public static partial void LoginInstructionsSendFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 1509, Level = LogLevel.Error, Message = "Login instructions not sent to external user {UserId}: App:BaseUrl is not a valid web address")]
    public static partial void LoginInstructionsLinkNotConfigured(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1510, Level = LogLevel.Warning, Message = "Login instructions were sent but not recorded: the email address of external user {UserId} changed during the send")]
    public static partial void LoginInstructionsEmailChanged(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1511, Level = LogLevel.Warning, Message = "ReviewNotifications:TestRecipient is set; the login instructions for external user {UserId} are sent to it instead of the user's own address")]
    public static partial void LoginInstructionsRedirectedToTestRecipient(this ILogger logger, Guid userId);
}
