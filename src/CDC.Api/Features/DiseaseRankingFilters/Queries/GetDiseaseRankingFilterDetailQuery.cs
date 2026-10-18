using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using MediatR;

namespace CDC.Api.Features.DiseaseRankingFilters.Queries;

/// <summary>Gets one disease ranking filter's full detail, to re-display it for editing.</summary>
/// <param name="FilterNumber">The filter to read.</param>
public sealed record GetDiseaseRankingFilterDetailQuery(long FilterNumber) : IRequest<Result<DiseaseRankingFilterDetailDto>>;

/// <summary>Handles <see cref="GetDiseaseRankingFilterDetailQuery"/>.</summary>
/// <param name="repository">Disease ranking filters data access.</param>
public sealed class GetDiseaseRankingFilterDetailQueryHandler(IDiseaseRankingFiltersRepository repository)
    : IRequestHandler<GetDiseaseRankingFilterDetailQuery, Result<DiseaseRankingFilterDetailDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The filter detail, or not-found when no active filter has that number.</returns>
    public async Task<Result<DiseaseRankingFilterDetailDto>> Handle(
        GetDiseaseRankingFilterDetailQuery request,
        CancellationToken cancellationToken)
    {
        var detail = await repository.GetFilterDetailAsync(request.FilterNumber, cancellationToken);

        if (detail is null)
        {
            return Result.NotFound<DiseaseRankingFilterDetailDto>(
                $"No active disease ranking filter exists with number '{request.FilterNumber}'.");
        }

        return Result.Success(new DiseaseRankingFilterDetailDto
        {
            FilterNumber = detail.FilterNumber,
            DiseaseName = detail.DiseaseName,
            Disease1 = detail.Disease1,
            Disease2 = detail.Disease2,
            Disease3 = detail.Disease3,
            Disease4 = detail.Disease4,
            DiseaseTypes = detail.DiseaseTypes,
            SpeciesIds = detail.SpeciesIds
        });
    }
}
