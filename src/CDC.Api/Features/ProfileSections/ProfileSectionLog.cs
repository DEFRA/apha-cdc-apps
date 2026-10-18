namespace CDC.Api.Features.ProfileSections;

/// <summary>Source-generated structured log messages for the profile sections feature.</summary>
internal static partial class ProfileSectionLog
{
    [LoggerMessage(EventId = 9000, Level = LogLevel.Information, Message = "Retrieved profile questionnaire metadata containing {SectionCount} sections")]
    public static partial void RetrievedProfileQuestionnaireMetadata(this ILogger logger, int sectionCount);

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "Retrieved answers for profile version {ProfileVersionId} section {ProfileSectionId} containing {FieldValueCount} field values")]
    public static partial void RetrievedProfileSectionAnswers(this ILogger logger, Guid profileVersionId, Guid profileSectionId, int fieldValueCount);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Information, Message = "Retrieved validation status for profile version {ProfileVersionId} containing {SectionCount} sections")]
    public static partial void RetrievedProfileVersionValidationStatus(this ILogger logger, Guid profileVersionId, int sectionCount);

    [LoggerMessage(EventId = 9004, Level = LogLevel.Information, Message = "Cleared {FieldsClearedCount} field values for profile version {ProfileVersionId} section {ProfileSectionId}")]
    public static partial void ClearedProfileVersionSection(this ILogger logger, Guid profileVersionId, Guid profileSectionId, int fieldsClearedCount);

    [LoggerMessage(EventId = 9005, Level = LogLevel.Information, Message = "Validated profile version {ProfileVersionId} section {ProfileSectionId}: {ValidationErrorCount} validation errors")]
    public static partial void ValidatedProfileVersionSection(this ILogger logger, Guid profileVersionId, Guid profileSectionId, int validationErrorCount);

    [LoggerMessage(EventId = 9006, Level = LogLevel.Information, Message = "Saved {FieldsSavedCount} field values for profile version {ProfileVersionId} section {ProfileSectionId}")]
    public static partial void SavedProfileVersionSection(this ILogger logger, Guid profileVersionId, Guid profileSectionId, int fieldsSavedCount);

    [LoggerMessage(EventId = 9007, Level = LogLevel.Information, Message = "Saved revision dates for {SectionsSavedCount} of {SectionsSubmittedCount} submitted sections of profile version {ProfileVersionId}")]
    public static partial void SavedProfileVersionRevisionDates(this ILogger logger, Guid profileVersionId, int sectionsSavedCount, int sectionsSubmittedCount);

    [LoggerMessage(EventId = 9008, Level = LogLevel.Warning, Message = "Revision dates of profile version {ProfileVersionId} were not saved: a section was edited by another user or the version is not a current draft")]
    public static partial void RevisionDatesConcurrencyConflict(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 9009, Level = LogLevel.Information, Message = "Added review comment {ReviewCommentId} to profile version {ProfileVersionId} section {ProfileSectionId}")]
    public static partial void AddedReviewComment(this ILogger logger, Guid reviewCommentId, Guid profileVersionId, Guid profileSectionId);

    [LoggerMessage(EventId = 9010, Level = LogLevel.Information, Message = "Updated review comment {ReviewCommentId}")]
    public static partial void UpdatedReviewComment(this ILogger logger, Guid reviewCommentId);

    [LoggerMessage(EventId = 9011, Level = LogLevel.Information, Message = "Deleted review comment {ReviewCommentId}")]
    public static partial void DeletedReviewComment(this ILogger logger, Guid reviewCommentId);

    [LoggerMessage(EventId = 9012, Level = LogLevel.Warning, Message = "Review comment {ReviewCommentId} was not saved: it was edited by another user")]
    public static partial void ReviewCommentConcurrencyConflict(this ILogger logger, Guid reviewCommentId);

    [LoggerMessage(EventId = 9013, Level = LogLevel.Warning, Message = "Review comment change refused for profile version {ProfileVersionId}: {Reason}")]
    public static partial void ReviewCommentChangeRefused(this ILogger logger, Guid profileVersionId, string reason);

    [LoggerMessage(EventId = 9014, Level = LogLevel.Warning, Message = "Profile version {ProfileVersionId} section {ProfileSectionId} was not changed: it was saved by another user, or the version is not a current draft")]
    public static partial void ProfileSectionConcurrencyConflict(this ILogger logger, Guid profileVersionId, Guid profileSectionId);

    [LoggerMessage(EventId = 9015, Level = LogLevel.Information, Message = "Deleted every review comment of profile version {ProfileVersionId} section {ProfileSectionId}")]
    public static partial void DeletedAllReviewComments(this ILogger logger, Guid profileVersionId, Guid profileSectionId);
}
