using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using MediatR;

namespace CDC.Api.Features.DiseaseRankingFilters.Commands;

/// <summary>Soft-deletes a disease ranking filter, matching the legacy "Delete Filter" button.</summary>
/// <param name="FilterNumber">The filter to delete.</param>
public sealed record DeleteDiseaseRankingFilterCommand(long FilterNumber) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="DeleteDiseaseRankingFilterCommand"/>.</summary>
/// <param name="repository">Disease ranking filters data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class DeleteDiseaseRankingFilterCommandHandler(
    IDiseaseRankingFiltersRepository repository,
    ILogger<DeleteDiseaseRankingFilterCommandHandler> logger)
    : IRequestHandler<DeleteDiseaseRankingFilterCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the filter is deleted.</returns>
    public async Task<Result<Unit>> Handle(DeleteDiseaseRankingFilterCommand request, CancellationToken cancellationToken)
    {
        await repository.DeleteFilterAsync(request.FilterNumber, cancellationToken);

        logger.DeletedFilter(request.FilterNumber);

        return Result.Success(Unit.Value);
    }
}
