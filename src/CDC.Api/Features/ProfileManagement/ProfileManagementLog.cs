namespace CDC.Api.Features.ProfileManagement;

/// <summary>
/// Source-generated structured log messages for the profile management feature. Nothing here
/// records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class ProfileManagementLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Created profile {ProfileId}")]
    public static partial void CreatedProfile(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Updated attributes for profile {ProfileId}")]
    public static partial void UpdatedProfileAttributes(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Rejected a concurrent update to profile {ProfileId}")]
    public static partial void ConcurrencyConflict(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Deleted profile version {ProfileVersionId}")]
    public static partial void DeletedProfileVersion(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "No profile version found for {ProfileVersionId}; nothing deleted")]
    public static partial void ProfileVersionNotFoundForDeletion(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Created profile version {NewProfileVersionId} from {ProfileVersionId}")]
    public static partial void CreatedNewProfileVersion(this ILogger logger, Guid newProfileVersionId, Guid profileVersionId);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Warning, Message = "Rejected creating a new version from {ProfileVersionId}: {Reason}")]
    public static partial void NewProfileVersionRejected(this ILogger logger, Guid profileVersionId, string reason);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Information, Message = "Retrieved attributes for profile {ProfileId}")]
    public static partial void RetrievedProfileAttributes(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 3008, Level = LogLevel.Information, Message = "No profile found for {ProfileId}")]
    public static partial void ProfileNotFound(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 3009, Level = LogLevel.Information, Message = "Retrieved new profile defaults from {CloneProfileVersionId}")]
    public static partial void RetrievedNewProfileDefaults(this ILogger logger, Guid cloneProfileVersionId);

    [LoggerMessage(EventId = 3010, Level = LogLevel.Information, Message = "Retrieved {StatusTypeCount} profile status types")]
    public static partial void RetrievedProfileStatusTypes(this ILogger logger, int statusTypeCount);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Information, Message = "Toggled public access for profile version {ProfileVersionId}")]
    public static partial void TogglePublicAccess(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Information, Message = "Set status {ProfileStatusId} on profile {ProfileId}")]
    public static partial void UpdatedProfileStatus(this ILogger logger, Guid profileId, Guid profileStatusId);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
