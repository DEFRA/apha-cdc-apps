namespace CDC.Api.Features.Users;

/// <summary>
/// Source-generated structured log messages for CIDM external-user and Entra ID internal-user
/// resolution. Nothing here records personal data: only identifiers are logged, never names,
/// email addresses or user names.
/// </summary>
internal static partial class UserLog
{
    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "Matched external user {UserId} by CIDM SSO id")]
    public static partial void MatchedBySsoUserIdExt(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Matched external user {UserId} by email on first CIDM sign-in; backfilling CIDM SSO id")]
    public static partial void MatchedByEmailBackfillingSsoUserIdExt(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Warning, Message = "Email claim matched user {UserId}, which is not an external user - denying CIDM sign-in")]
    public static partial void EmailMatchedNonExternalUser(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Information, Message = "No existing user matched the CIDM SSO id or email claim; creating external user {UserId}")]
    public static partial void CreatedNewExternalUser(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Information, Message = "Matched internal user {UserId} by Entra SSO id")]
    public static partial void MatchedBySsoUserIdInt(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "Matched internal user {UserId} by user name on first Entra sign-in; backfilling Entra SSO id")]
    public static partial void MatchedByUserNameBackfillingSsoUserIdInt(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8007, Level = LogLevel.Information, Message = "No existing user matched the Entra SSO id or user name claim; granting limited (no-privilege) internal access")]
    public static partial void GrantedLimitedAccessForUnmatchedInternalUser(this ILogger logger);
}

