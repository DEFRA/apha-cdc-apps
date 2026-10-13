namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the profile section tab pages.</summary>
internal static partial class ProfileSectionTabLog
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' questions from CDC.Api")]
    public static partial void ProfileQuestionsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' references/further information from CDC.Api")]
    public static partial void ProfileNotesLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' section validation status from CDC.Api")]
    public static partial void ProfileSectionValidationStatusLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2303, Level = LogLevel.Error, Message = "Failed to validate profile '{ProfileId}' section '{ProfileSectionId}' via CDC.Api")]
    public static partial void ProfileSectionValidateFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileSectionId);

    [LoggerMessage(EventId = 2304, Level = LogLevel.Error, Message = "Failed to clear profile '{ProfileId}' section '{ProfileSectionId}' via CDC.Api")]
    public static partial void ProfileSectionClearFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileSectionId);

    [LoggerMessage(EventId = 2305, Level = LogLevel.Error, Message = "Failed to save profile '{ProfileId}' section '{ProfileSectionId}' via CDC.Api")]
    public static partial void ProfileSectionSaveFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileSectionId);

    [LoggerMessage(EventId = 2306, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' contributions from CDC.Api")]
    public static partial void ProfileSectionContributionsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2307, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' review comments from CDC.Api")]
    public static partial void ProfileReviewCommentsLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2308, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' all revision dates from CDC.Api")]
    public static partial void ProfileAllRevisionDatesLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2309, Level = LogLevel.Error, Message = "Failed to save profile '{ProfileId}' all revision dates via CDC.Api")]
    public static partial void ProfileRevisionDatesSaveFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2310, Level = LogLevel.Warning, Message = "Profile '{ProfileId}' all revision dates were not saved because of a concurrent edit or a non-editable version")]
    public static partial void ProfileRevisionDatesSaveConflict(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 2311, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' from CDC.Api")]
    public static partial void ProfilePageLoadFailed(this ILogger logger, Exception exception, Guid profileId);
}
