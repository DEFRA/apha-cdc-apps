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

    [LoggerMessage(EventId = 8004, Level = LogLevel.Warning, Message = "CIDM sign-in denied: the account (SSO id {SsoUserIdExt}) is not authorised because no pre-registered user matches its SSO id or email address")]
    public static partial void ExternalUserNotPreRegistered(this ILogger logger, Guid ssoUserIdExt);

    [LoggerMessage(EventId = 8009, Level = LogLevel.Information, Message = "Internal profile editor {UserId} matched by email on CIDM sign-in; signing in without storing a CIDM SSO id")]
    public static partial void InternalProfileEditorSignedInThroughCidm(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8010, Level = LogLevel.Information, Message = "Recorded the email address of internal user {UserId} from the Entra sign-in")]
    public static partial void RecordedInternalUserEmailAddress(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8011, Level = LogLevel.Warning, Message = "Could not record the email address of internal user {UserId}; signing in without it")]
    public static partial void FailedToRecordInternalUserEmailAddress(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Information, Message = "Matched internal user {UserId} by Entra SSO id")]
    public static partial void MatchedBySsoUserIdInt(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "Matched internal user {UserId} by user name on first Entra sign-in; backfilling Entra SSO id")]
    public static partial void MatchedByUserNameBackfillingSsoUserIdInt(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 8008, Level = LogLevel.Information, Message = "No existing user matched the Entra SSO id or user name claim; creating internal user {UserId}")]
    public static partial void CreatedNewInternalUser(this ILogger logger, Guid userId);
}

