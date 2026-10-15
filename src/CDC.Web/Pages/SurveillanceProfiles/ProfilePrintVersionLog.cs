namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the profile print version download.</summary>
internal static partial class ProfilePrintVersionLog
{
    [LoggerMessage(EventId = 2530, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' from CDC.Api to print it")]
    public static partial void ProfilePrintVersionLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2531, Level = LogLevel.Error, Message = "Failed to build the print version of profile '{ProfileId}' (profile version '{ProfileVersionId}')")]
    public static partial void ProfilePrintVersionBuildFailed(this ILogger logger, Exception exception, Guid profileId, Guid profileVersionId);

    [LoggerMessage(EventId = 2532, Level = LogLevel.Warning, Message = "The print version of profile '{ProfileId}' was refused because the user may not print its current version")]
    public static partial void ProfilePrintVersionRefused(this ILogger logger, Guid profileId);
}
