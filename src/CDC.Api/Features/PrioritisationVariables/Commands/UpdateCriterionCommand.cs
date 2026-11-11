using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Commands;

/// <summary>
/// One criterion value's new score, as part of <see cref="UpdateCriterionCommand"/>.
/// </summary>
/// <param name="ValueId">The criterion value to update.</param>
/// <param name="Score">The new score.</param>
public sealed record CriterionValueScore(Guid ValueId, int Score);

/// <summary>
/// Updates a prioritisation criterion's weighting and all of its value scores together - mirrors
/// the legacy criterion edit panel, which saves the weighting and every value's score in one action.
/// </summary>
/// <param name="CriterionId">The criterion to update.</param>
/// <param name="Weight">The new weighting.</param>
/// <param name="ValueScores">The new score for every value belonging to the criterion.</param>
/// <param name="Name">The new criterion name, or <see langword="null"/> to keep the current one.</param>
public sealed record UpdateCriterionCommand(Guid CriterionId, int Weight, IReadOnlyList<CriterionValueScore> ValueScores, string? Name = null)
    : IRequest<Result<Unit>>;

/// <summary>
/// Handles <see cref="UpdateCriterionCommand"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
public sealed class UpdateCriterionCommandHandler(IPrioritisationVariablesRepository repository)
    : IRequestHandler<UpdateCriterionCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the weighting and value scores are saved, or not-found when the criterion does not exist.</returns>
    public async Task<Result<Unit>> Handle(UpdateCriterionCommand request, CancellationToken cancellationToken)
    {
        // spuPrioritisationCriterion always sets Name, so the current one is read when no new name is supplied.
        var categories = await repository.GetCategoriesWithCriteriaAsync(cancellationToken);
        var criterion = categories
            .SelectMany(category => category.Criteria)
            .FirstOrDefault(criterion => criterion.Id == request.CriterionId);

        if (criterion is null)
        {
            return Result.NotFound<Unit>($"No prioritisation criterion exists with id '{request.CriterionId}'.");
        }

        await repository.UpdateCriterionAsync(request.CriterionId, request.Name ?? criterion.Name, request.Weight, cancellationToken);

        foreach (var valueScore in request.ValueScores)
        {
            await repository.UpdateCriterionValueScoreAsync(valueScore.ValueId, valueScore.Score, cancellationToken);
        }

        return Result.Success(Unit.Value);
    }
}
