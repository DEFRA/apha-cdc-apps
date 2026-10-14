using CDC.Web.Models;

namespace CDC.Web.Pages.DiseaseRanking;

/// <summary>
/// Source-generated structured log messages for <see cref="EditFiltersModel"/>. Nothing here
/// records personal data: only identifiers and outcome classifications are logged.
/// </summary>
internal static partial class EditFiltersLog
{
    [LoggerMessage(EventId = 3100, Level = LogLevel.Warning, Message = "Failed to load disease ranking filters or species")]
    public static partial void LoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Warning, Message = "Disease ranking filter save failed for filter {FilterNumber}: {Outcome}")]
    public static partial void SaveFailed(this ILogger logger, long? filterNumber, DiseaseRankingFilterSaveOutcome outcome);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Saved disease ranking filter {FilterNumber} (new: {IsNewFilter})")]
    public static partial void Saved(this ILogger logger, long filterNumber, bool isNewFilter);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Information, Message = "Deleted disease ranking filter {FilterNumber}")]
    public static partial void Deleted(this ILogger logger, long filterNumber);
}
