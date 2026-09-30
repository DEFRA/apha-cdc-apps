namespace CDC.Api.Features.ProfileNotes;

/// <summary>
/// Source-generated structured log messages for the profile notes feature. Nothing here
/// records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class ProfileNoteLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Retrieved {NoteTypeCount} profile note types")]
    public static partial void RetrievedNoteTypes(this ILogger logger, int noteTypeCount);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Retrieved {NoteCount} notes for section {ProfileSectionId} of profile version {ProfileVersionId}")]
    public static partial void RetrievedNotesBySection(this ILogger logger, int noteCount, Guid profileSectionId, Guid profileVersionId);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Retrieved {NoteCount} notes for profile version {ProfileVersionId}")]
    public static partial void RetrievedNotesByVersion(this ILogger logger, int noteCount, Guid profileVersionId);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Applied a note changeset to profile version {ProfileVersionId}")]
    public static partial void AppliedChangeset(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Warning, Message = "Rejected a concurrent change to profile version {ProfileVersionId}")]
    public static partial void ConcurrencyConflict(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
