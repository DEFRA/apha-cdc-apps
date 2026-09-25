namespace CDC.Api.Features.ProfileSearch;

/// <summary>
/// Source-generated structured log messages for the profile search feature. Nothing here
/// records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class ProfileSearchLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Retrieved {ProfileCount} profiles")]
    public static partial void RetrievedAllProfiles(this ILogger logger, int profileCount);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
