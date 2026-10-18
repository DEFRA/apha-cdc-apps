namespace CDC.Api.Features.DiseaseRankingFilters;

/// <summary>
/// Source-generated structured log messages for the disease ranking filters feature. Nothing
/// here records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class DiseaseRankingFiltersLog
{
    [LoggerMessage(EventId = 2200, Level = LogLevel.Information, Message = "Retrieved {FilterCount} disease ranking filters")]
    public static partial void RetrievedFilters(this ILogger logger, int filterCount);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "Disease ranking filter save rejected - name '{DiseaseName}' already in use")]
    public static partial void DuplicateName(this ILogger logger, string diseaseName);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Information, Message = "Saved disease ranking filter {FilterNumber} with {SpeciesCount} species")]
    public static partial void SavedFilter(this ILogger logger, long filterNumber, int speciesCount);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information, Message = "Deleted disease ranking filter {FilterNumber}")]
    public static partial void DeletedFilter(this ILogger logger, long filterNumber);
}
