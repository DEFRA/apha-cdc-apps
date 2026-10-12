namespace CDC.Api.Features.DiseaseRankingFilters.Dtos;

/// <summary>Wire contract for <c>POST /api/disease-ranking-filters</c> and
/// <c>PUT /api/disease-ranking-filters/{filterNumber}</c>.</summary>
public sealed record SaveDiseaseRankingFilterRequestDto
{
    /// <summary>Gets the filter's display name.</summary>
    public string DiseaseName { get; init; } = string.Empty;

    /// <summary>Gets the endemic/exotic/both criterion.</summary>
    public string Disease1 { get; init; } = string.Empty;

    /// <summary>Gets the zoonotic/non-zoonotic/both criterion.</summary>
    public string Disease2 { get; init; } = string.Empty;

    /// <summary>Gets the notifiable/non-notifiable/both criterion.</summary>
    public string Disease3 { get; init; } = string.Empty;

    /// <summary>Gets the infectious/non-infectious/both criterion.</summary>
    public string Disease4 { get; init; } = string.Empty;

    /// <summary>Gets the selected transmissible agent types.</summary>
    public IReadOnlyList<string> DiseaseTypes { get; init; } = [];

    /// <summary>Gets every species selected for this filter. Must contain at least one id.</summary>
    public IReadOnlyList<Guid> SpeciesIds { get; init; } = [];
}
