using CDC.Common.Contracts;

namespace CDC.Api.Features.DiseaseRanking.Interfaces;

/// <summary>Builds the disease ranking reports behind the legacy <c>DiseaseRanking.aspx</c> page.</summary>
public interface IDiseaseRankingService
{
    /// <summary>Gets the reports a user can choose from: terrestrial, aquatic, then each saved filter.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Builds one report.</summary>
    /// <param name="reportKey"><c>terrestrial</c>, <c>aquatic</c> or a saved filter id.</param>
    /// <param name="kind">Scored or ranked graphs.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report, or <see langword="null"/> when <paramref name="reportKey"/> is not a known report.</returns>
    Task<DiseaseRankingReportDto?> GetReportAsync(
        string reportKey,
        DiseaseRankingReportKind kind,
        CancellationToken cancellationToken);
}
