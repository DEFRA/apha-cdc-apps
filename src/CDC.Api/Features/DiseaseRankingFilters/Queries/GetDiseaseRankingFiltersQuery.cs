using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using MediatR;

namespace CDC.Api.Features.DiseaseRankingFilters.Queries;

/// <summary>Gets every active disease ranking filter, for the "Select Filter" dropdown.</summary>
public sealed record GetDiseaseRankingFiltersQuery : IRequest<Result<IReadOnlyList<DiseaseRankingFilterSummaryDto>>>;

/// <summary>Handles <see cref="GetDiseaseRankingFiltersQuery"/>.</summary>
/// <param name="repository">Disease ranking filters data access.</param>
public sealed class GetDiseaseRankingFiltersQueryHandler(IDiseaseRankingFiltersRepository repository)
    : IRequestHandler<GetDiseaseRankingFiltersQuery, Result<IReadOnlyList<DiseaseRankingFilterSummaryDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every active filter.</returns>
    public async Task<Result<IReadOnlyList<DiseaseRankingFilterSummaryDto>>> Handle(
        GetDiseaseRankingFiltersQuery request,
        CancellationToken cancellationToken)
    {
        var filters = await repository.GetFiltersAsync(cancellationToken);

        IReadOnlyList<DiseaseRankingFilterSummaryDto> dtos =
        [
            .. filters.Select(filter => new DiseaseRankingFilterSummaryDto
            {
                FilterNumber = filter.FilterNumber,
                DiseaseName = filter.DiseaseName
            })
        ];

        return Result.Success(dtos);
    }
}
