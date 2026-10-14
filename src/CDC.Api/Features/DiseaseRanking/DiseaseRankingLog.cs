using CDC.Common.Contracts;

namespace CDC.Api.Features.DiseaseRanking;

/// <summary>
/// Source-generated structured log messages for the disease ranking feature. Only identifiers and
/// counts are logged.
/// </summary>
internal static partial class DiseaseRankingLog
{
    [LoggerMessage(EventId = 9100, Level = LogLevel.Information, Message = "Built {Kind} disease ranking report {ReportKey} from {ProfileCount} ranked profiles")]
    public static partial void BuiltReport(this ILogger logger, DiseaseRankingReportKind kind, string reportKey, int profileCount);

    [LoggerMessage(EventId = 9101, Level = LogLevel.Warning, Message = "Unknown disease ranking report {ReportKey}")]
    public static partial void UnknownReport(this ILogger logger, string reportKey);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
