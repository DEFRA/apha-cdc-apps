using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Queries;

/// <summary>Builds the "Export score data" CSV for one prioritisation category.</summary>
/// <param name="CategoryId">The category to export.</param>
public sealed record ExportCategoryScoreDataQuery(Guid CategoryId) : IRequest<Result<PrioritisationCsvExport>>;

/// <summary>Builds the "Export normalised scores (all categories)" CSV.</summary>
public sealed record ExportNormalisedScoresQuery : IRequest<Result<PrioritisationCsvExport>>;

/// <summary>
/// Handles <see cref="ExportCategoryScoreDataQuery"/> and <see cref="ExportNormalisedScoresQuery"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
/// <param name="rankingRepository">Reads the disease ranks used by the normalised scores export.</param>
public sealed class ExportPrioritisationScoresQueryHandler(
    IPrioritisationVariablesRepository repository,
    IDiseaseRankingRepository rankingRepository)
    : IRequestHandler<ExportCategoryScoreDataQuery, Result<PrioritisationCsvExport>>,
      IRequestHandler<ExportNormalisedScoresQuery, Result<PrioritisationCsvExport>>
{
    /// <summary>Executes the category query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The CSV file, or not-found when the category does not exist.</returns>
    public async Task<Result<PrioritisationCsvExport>> Handle(ExportCategoryScoreDataQuery request, CancellationToken cancellationToken)
    {
        var data = await repository.GetScoreDataAsync(cancellationToken);
        var export = PrioritisationScoreCsvBuilder.BuildCategoryScoreData(data, request.CategoryId);

        return export is null
            ? Result.NotFound<PrioritisationCsvExport>($"No prioritisation category exists with id '{request.CategoryId}'.")
            : Result.Success(export);
    }

    /// <summary>Executes the all-categories query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The CSV file.</returns>
    public async Task<Result<PrioritisationCsvExport>> Handle(ExportNormalisedScoresQuery request, CancellationToken cancellationToken)
    {
        var data = await repository.GetScoreDataAsync(cancellationToken);
        var rankedProfiles = await rankingRepository.GetRankedProfilesAsync(null, cancellationToken);

        return Result.Success(PrioritisationScoreCsvBuilder.BuildNormalisedScores(data, rankedProfiles));
    }
}
