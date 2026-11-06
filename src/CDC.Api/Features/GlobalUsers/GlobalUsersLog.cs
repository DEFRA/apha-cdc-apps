using CDC.Common.Contracts;

namespace CDC.Api.Features.GlobalUsers;

/// <summary>
/// Source-generated structured log messages for the global users feature. Nothing here records
/// personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class GlobalUsersLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Information, Message = "Retrieved {UserCount} global users")]
    public static partial void RetrievedGlobalUsers(this ILogger logger, int userCount);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Information, Message = "Global user lookup finished with outcome {Outcome}")]
    public static partial void LookupCompleted(this ILogger logger, GlobalUserLookupOutcome outcome);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Information, Message = "Saving global user {UserId}")]
    public static partial void SavingGlobalUser(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1404, Level = LogLevel.Information, Message = "Saved global user {UserId}")]
    public static partial void SavedGlobalUser(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1405, Level = LogLevel.Warning, Message = "Rejected a concurrent edit to global user {UserId}")]
    public static partial void GlobalUserConcurrencyConflict(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1406, Level = LogLevel.Warning, Message = "Rejected a duplicate username saving global user {UserId}")]
    public static partial void GlobalUserDuplicateUsername(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1407, Level = LogLevel.Information, Message = "Revoking global role for user {UserId}")]
    public static partial void RevokingGlobalUser(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1408, Level = LogLevel.Information, Message = "Revoked global role for user {UserId}")]
    public static partial void RevokedGlobalUser(this ILogger logger, Guid userId);
}
