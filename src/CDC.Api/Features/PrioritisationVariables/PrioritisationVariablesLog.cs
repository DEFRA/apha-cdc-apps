namespace CDC.Api.Features.PrioritisationVariables;

/// <summary>
/// Source-generated structured log messages for the prioritisation variables feature. Nothing
/// here records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class PrioritisationVariablesLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Retrieved {CategoryCount} prioritisation categories")]
    public static partial void RetrievedCategories(this ILogger logger, int categoryCount);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "Ranking range update rejected - row version conflict")]
    public static partial void RankingRangeConcurrencyConflict(this ILogger logger);
}
