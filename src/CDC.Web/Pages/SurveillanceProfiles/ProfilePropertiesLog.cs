namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the Profile properties page.</summary>
internal static partial class ProfilePropertiesLog
{
    [LoggerMessage(EventId = 2540, Level = LogLevel.Warning, Message = "Profile '{ProfileId}' was not found when opening its properties")]
    public static partial void ProfileNotFound(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 2541, Level = LogLevel.Warning, Message = "Profile version '{CloneProfileVersionId}' was not found when cloning it")]
    public static partial void CloneSourceNotFound(this ILogger logger, Guid cloneProfileVersionId);

    [LoggerMessage(EventId = 2542, Level = LogLevel.Error, Message = "Failed to load the Profile properties page data from CDC.Api")]
    public static partial void LoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2543, Level = LogLevel.Warning, Message = "Profile properties for '{ProfileId}' cannot be opened: {Reason}")]
    public static partial void NotEditable(this ILogger logger, Guid? profileId, string reason);

    [LoggerMessage(EventId = 2544, Level = LogLevel.Information, Message = "Created profile '{ProfileId}' (cloned from version '{CloneProfileVersionId}', scenario: {IsScenario})")]
    public static partial void ProfileCreated(this ILogger logger, Guid profileId, Guid cloneProfileVersionId, bool isScenario);

    [LoggerMessage(EventId = 2545, Level = LogLevel.Warning, Message = "CDC.Api refused to create a profile with outcome {Outcome}")]
    public static partial void CreateRefused(this ILogger logger, CDC.Web.Models.SaveProfileOutcome outcome);

    [LoggerMessage(EventId = 2546, Level = LogLevel.Information, Message = "Updated the properties of profile '{ProfileId}'")]
    public static partial void ProfileUpdated(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 2547, Level = LogLevel.Warning, Message = "CDC.Api refused to update profile '{ProfileId}' with outcome {Outcome}")]
    public static partial void UpdateRefused(this ILogger logger, Guid profileId, CDC.Web.Models.SaveProfileOutcome outcome);

    [LoggerMessage(EventId = 2548, Level = LogLevel.Error, Message = "Failed to save the properties of profile '{ProfileId}' via CDC.Api")]
    public static partial void SaveFailed(this ILogger logger, Exception exception, Guid? profileId);
}
