namespace CDC.Web.Models;

/// <summary>One entry in <c>GET /api/disease-ranking-filters</c> on CDC.Api, for the "Select Filter" dropdown.</summary>
public sealed record DiseaseRankingFilterSummaryDto
{
    /// <summary>Gets the filter's identifying number.</summary>
    public long FilterNumber { get; init; }

    /// <summary>Gets the filter's display name.</summary>
    public string DiseaseName { get; init; } = string.Empty;
}
