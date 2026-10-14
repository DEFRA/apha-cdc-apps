namespace CDC.Api.Features.DiseaseRankingFilters.Dtos;

/// <summary>Wire contract for one entry in <c>GET /api/disease-ranking-filters</c>.</summary>
public sealed record DiseaseRankingFilterSummaryDto
{
    /// <summary>Gets the filter's identifying number.</summary>
    public long FilterNumber { get; init; }

    /// <summary>Gets the filter's display name.</summary>
    public string DiseaseName { get; init; } = string.Empty;
}
