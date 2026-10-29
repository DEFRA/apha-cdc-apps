namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the edit user guidance page. Only identifiers are logged.</summary>
internal static partial class EditUserGuidanceLog
{
    [LoggerMessage(EventId = 2560, Level = LogLevel.Error, Message = "Failed to load the profile sections for the edit user guidance page from CDC.Api")]
    public static partial void SectionsLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2561, Level = LogLevel.Error, Message = "Failed to load the questions for profile section '{SectionId}' from CDC.Api")]
    public static partial void QuestionsLoadFailed(this ILogger logger, Exception exception, Guid sectionId);

    [LoggerMessage(EventId = 2562, Level = LogLevel.Error, Message = "Failed to load profile question '{QuestionId}' for editing from CDC.Api")]
    public static partial void QuestionLoadFailed(this ILogger logger, Exception exception, Guid questionId);

    [LoggerMessage(EventId = 2563, Level = LogLevel.Error, Message = "Failed to save profile question '{QuestionId}' through CDC.Api")]
    public static partial void QuestionSaveFailed(this ILogger logger, Exception exception, Guid questionId);

    [LoggerMessage(EventId = 2564, Level = LogLevel.Warning, Message = "CDC.Api did not save profile question '{QuestionId}': {Outcome}")]
    public static partial void QuestionSaveRejected(this ILogger logger, Guid questionId, CDC.Web.Models.ProfileQuestionUpdateOutcome outcome);
}
