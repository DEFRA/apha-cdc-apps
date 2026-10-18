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

    [LoggerMessage(EventId = 1503, Level = LogLevel.Information, Message = "Saved profile access for external user {UserId} on profile {ProfileId}")]
    public static partial void ProfileAccessSaved(this ILogger logger, Guid userId, Guid profileId);

    [LoggerMessage(EventId = 1504, Level = LogLevel.Information, Message = "Removed profile access for external user {UserId} on profile {ProfileId}")]
    public static partial void ProfileAccessRemoved(this ILogger logger, Guid userId, Guid profileId);
}
