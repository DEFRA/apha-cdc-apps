using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using MediatR;

namespace CDC.Api.Features.DiseaseRankingFilters.Commands;

/// <summary>
/// Creates a new disease ranking filter, or replaces the species/criteria for an existing one.
/// Mirrors the legacy <c>EditDiseaseRankingFilters.aspx</c> "Submit"/"Update" button, which saves
/// the filter name, every criterion and the full species selection together.
/// </summary>
/// <param name="FilterNumber">The filter to update, or <see langword="null"/> to create a new filter.</param>
/// <param name="Request">The filter's name, criteria and selected species.</param>
public sealed record SaveDiseaseRankingFilterCommand(long? FilterNumber, SaveDiseaseRankingFilterRequestDto Request)
    : IRequest<Result<DiseaseRankingFilterDetailDto>>;

/// <summary>Handles <see cref="SaveDiseaseRankingFilterCommand"/>.</summary>
/// <param name="repository">Disease ranking filters data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SaveDiseaseRankingFilterCommandHandler(
    IDiseaseRankingFiltersRepository repository,
    ILogger<SaveDiseaseRankingFilterCommandHandler> logger)
    : IRequestHandler<SaveDiseaseRankingFilterCommand, Result<DiseaseRankingFilterDetailDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The saved filter on success; a conflict result when creating a filter whose name is
    /// already in use by another active filter.
    /// </returns>
    public async Task<Result<DiseaseRankingFilterDetailDto>> Handle(
        SaveDiseaseRankingFilterCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;

        long filterNumber;

        if (command.FilterNumber is { } existingFilterNumber)
        {
            filterNumber = existingFilterNumber;

            // spiUpdateDiseaseFilterValue only reactivates and updates a row it finds already
            // disabled (IsInUse = 0) for the given species - it does nothing for a species whose
            // row is still active, which is every unchanged species in the new selection.
            // Disabling every row for this filter first means each currently selected species -
            // unchanged, newly added, or previously removed and now re-added - gets a fresh
            // active row with the new criteria, while species no longer selected stay disabled.
            await repository.DeleteFilterAsync(filterNumber, cancellationToken);

            foreach (var speciesId in request.SpeciesIds)
            {
                await repository.UpdateFilterSpeciesAsync(filterNumber, request, speciesId, cancellationToken);
            }
        }
        else
        {
            filterNumber = await repository.ReserveFilterNumberAsync(request.DiseaseName, cancellationToken);

            if (filterNumber <= 0)
            {
                logger.DuplicateName(request.DiseaseName);
                return Result.Conflict<DiseaseRankingFilterDetailDto>(
                    $"A disease ranking filter named '{request.DiseaseName}' already exists.");
            }

            foreach (var speciesId in request.SpeciesIds)
            {
                await repository.InsertFilterSpeciesAsync(filterNumber, request, speciesId, cancellationToken);
            }
        }

        logger.SavedFilter(filterNumber, request.SpeciesIds.Count);

        return Result.Success(new DiseaseRankingFilterDetailDto
        {
            FilterNumber = filterNumber,
            DiseaseName = request.DiseaseName,
            Disease1 = request.Disease1,
            Disease2 = request.Disease2,
            Disease3 = request.Disease3,
            Disease4 = request.Disease4,
            DiseaseTypes = request.DiseaseTypes,
            SpeciesIds = request.SpeciesIds
        });
    }
}
