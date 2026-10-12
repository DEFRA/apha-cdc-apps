using CDC.Common.Contracts;

namespace CDC.Api.Features.DiseaseRankingFilters.Dtos;

/// <summary>Wire contract for <c>POST /api/disease-ranking-filters</c> and
/// <c>PUT /api/disease-ranking-filters/{filterNumber}</c>.</summary>
public sealed record SaveDiseaseRankingFilterRequestDto : SaveDiseaseRankingFilterRequestContract; // NOSONAR
