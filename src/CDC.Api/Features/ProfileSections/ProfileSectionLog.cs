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
}
