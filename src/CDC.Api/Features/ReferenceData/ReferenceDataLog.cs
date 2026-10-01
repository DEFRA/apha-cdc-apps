namespace CDC.Api.Features.ReferenceData;

/// <summary>Source-generated structured log messages for the reference data feature.</summary>
internal static partial class ReferenceDataLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Retrieved {ValueCount} values for reference table {ReferenceTableId}")]
    public static partial void RetrievedReferenceValues(this ILogger logger, int valueCount, Guid referenceTableId);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
