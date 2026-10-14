using CDC.Common.Contracts;

namespace CDC.Web.Infrastructure;

/// <summary>Reads disease ranking reports from CDC.Api.</summary>
public interface IDiseaseRankingApiService
{
    /// <summary>Gets the reports a user can choose from, via <c>GET /api/disease-ranking/report-options</c>.</summary>
    Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets one report via <c>GET /api/disease-ranking/reports/{reportKey}</c>.</summary>
    /// <returns>The report, or <see langword="null"/> when no such report exists.</returns>
    Task<DiseaseRankingReportDto?> GetReportAsync(
        string reportKey,
        DiseaseRankingReportKind kind,
        CancellationToken cancellationToken = default);
}
