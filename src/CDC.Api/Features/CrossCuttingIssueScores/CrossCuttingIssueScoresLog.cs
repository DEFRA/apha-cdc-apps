namespace CDC.Api.Features.CrossCuttingIssueScores;

/// <summary>Source-generated structured log messages for the cross-cutting issue scores feature.</summary>
internal static partial class CrossCuttingIssueScoresLog
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Information, Message = "Retrieved {CategoryCount} cross-cutting issues")]
    public static partial void RetrievedCategories(this ILogger logger, int categoryCount);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Information, Message = "Saved {ValueCount} cross-cutting issue scores and recalculated species prioritisation")]
    public static partial void CrossCuttingIssueScoresSaved(this ILogger logger, int valueCount);
}
