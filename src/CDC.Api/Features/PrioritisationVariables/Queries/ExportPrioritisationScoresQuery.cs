using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Queries;

/// <summary>Builds the "Export score data" Excel file for one prioritisation category.</summary>
/// <param name="CategoryId">The category to export.</param>
public sealed record ExportCategoryScoreDataQuery(Guid CategoryId) : IRequest<Result<PrioritisationWorkbookExport>>;

/// <summary>Builds the "Export normalised scores (all categories)" Excel file.</summary>
public sealed record ExportNormalisedScoresQuery : IRequest<Result<PrioritisationWorkbookExport>>;

/// <summary>
/// Handles <see cref="ExportCategoryScoreDataQuery"/> and <see cref="ExportNormalisedScoresQuery"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
/// <param name="rankingRepository">Reads the disease ranks used by the normalised scores export.</param>
public sealed class ExportPrioritisationScoresQueryHandler(
    IPrioritisationVariablesRepository repository,
    IDiseaseRankingRepository rankingRepository)
    : IRequestHandler<ExportCategoryScoreDataQuery, Result<PrioritisationWorkbookExport>>,
      IRequestHandler<ExportNormalisedScoresQuery, Result<PrioritisationWorkbookExport>>
{
    /// <summary>Executes the category query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The Excel file, or not-found when the category does not exist.</returns>
    public async Task<Result<PrioritisationWorkbookExport>> Handle(ExportCategoryScoreDataQuery request, CancellationToken cancellationToken)
    {
        var data = await repository.GetScoreDataAsync(cancellationToken);
        var export = PrioritisationScoreWorkbookBuilder.BuildCategoryScoreData(data, request.CategoryId);

        return export is null
            ? Result.NotFound<PrioritisationWorkbookExport>($"No prioritisation category exists with id '{request.CategoryId}'.")
            : Result.Success(export);
    }

    /// <summary>Executes the all-categories query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The Excel file.</returns>
    public async Task<Result<PrioritisationWorkbookExport>> Handle(ExportNormalisedScoresQuery request, CancellationToken cancellationToken)
    {
        var data = await repository.GetScoreDataAsync(cancellationToken);
        var rankedProfiles = await rankingRepository.GetRankedProfilesAsync(null, cancellationToken);

        return Result.Success(PrioritisationScoreWorkbookBuilder.BuildNormalisedScores(data, rankedProfiles));
    }
}
