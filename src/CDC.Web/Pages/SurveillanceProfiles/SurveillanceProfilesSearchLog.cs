namespace CDC.Web.Pages;

/// <summary>
/// Source-generated structured log messages for <see cref="SurveillanceProfilesSearchModel"/>.
/// Nothing here records personal data: only counts and filter values are logged.
/// </summary>
internal static partial class SurveillanceProfilesSearchLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Error, Message = "Failed to load species filter values")]
    public static partial void SpeciesFilterLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Profile search completed with 0 results after applying filter selections")]
    public static partial void ProfileSearchExcludedAllStatuses(this ILogger logger);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Profile search completed with {ResultCount} results")]
    public static partial void ProfileSearchCompleted(this ILogger logger, int resultCount);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error, Message = "Failed to retrieve profile search results")]
    public static partial void ProfileSearchRequestFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error, Message = "An unexpected error occurred while searching profiles")]
    public static partial void ProfileSearchUnexpectedError(this ILogger logger, Exception exception);
}
