using CDC.Web.Models;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the question help and references dialogs of the Questions tab. Only identifiers are logged.</summary>
internal static partial class QuestionDialogLog
{
    [LoggerMessage(EventId = 2800, Level = LogLevel.Error, Message = "Failed to load the guidance of profile question '{QuestionId}' from CDC.Api")]
    public static partial void QuestionHelpLoadFailed(this ILogger logger, Exception exception, Guid questionId);

    [LoggerMessage(EventId = 2801, Level = LogLevel.Error, Message = "Failed to load the reference types for the question references dialog of profile '{ProfileId}' from CDC.Api")]
    public static partial void ReferenceNoteTypesLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2802, Level = LogLevel.Error, Message = "Failed to load the references of type '{NoteTypeId}' for profile '{ProfileId}' from CDC.Api")]
    public static partial void ReferenceNotesLoadFailed(this ILogger logger, Exception exception, Guid profileId, Guid noteTypeId);

    [LoggerMessage(EventId = 2803, Level = LogLevel.Error, Message = "Failed to save a reference of type '{NoteTypeId}' on profile '{ProfileId}' through CDC.Api")]
    public static partial void ReferenceSaveFailed(this ILogger logger, Exception exception, Guid profileId, Guid noteTypeId);

    [LoggerMessage(EventId = 2804, Level = LogLevel.Warning, Message = "A reference change on profile '{ProfileId}' was refused by CDC.Api with outcome {Outcome}")]
    public static partial void ReferenceSaveRefused(this ILogger logger, Guid profileId, ProfileNoteChangeOutcome outcome);
}
