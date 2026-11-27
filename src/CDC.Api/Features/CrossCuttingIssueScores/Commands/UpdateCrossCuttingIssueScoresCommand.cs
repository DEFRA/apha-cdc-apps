using CDC.Api.Domain.Common;
using CDC.Api.Features.CrossCuttingIssueScores.Dtos;
using CDC.Api.Features.CrossCuttingIssueScores.Interfaces;
using MediatR;

namespace CDC.Api.Features.CrossCuttingIssueScores.Commands;

/// <summary>Commits changed criterion-value scores and recalculates every species prioritisation score.</summary>
/// <param name="Scores">The changed scores.</param>
public sealed record UpdateCrossCuttingIssueScoresCommand(IReadOnlyList<CrossCuttingIssueValueScoreDto> Scores)
    : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="UpdateCrossCuttingIssueScoresCommand"/>.</summary>
/// <param name="repository">Cross-cutting issue scores data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateCrossCuttingIssueScoresCommandHandler(
    ICrossCuttingIssueScoresRepository repository,
    ILogger<UpdateCrossCuttingIssueScoresCommandHandler> logger)
    : IRequestHandler<UpdateCrossCuttingIssueScoresCommand, Result<Unit>>
{
    private const int MinScore = 0;
    private const int MaxScore = 100;

    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once saved, validation-failed for an out-of-range score, or not-found for an unknown value.</returns>
    public async Task<Result<Unit>> Handle(UpdateCrossCuttingIssueScoresCommand request, CancellationToken cancellationToken)
    {
        if (request.Scores.Any(score => score.Score is < MinScore or > MaxScore))
        {
            return Result.ValidationFailed<Unit>($"Scores must be between {MinScore} and {MaxScore}.");
        }

        var categories = await repository.GetCategoriesAsync(cancellationToken);
        var knownValueIds = categories
            .SelectMany(category => category.Criteria)
            .SelectMany(criterion => criterion.Values)
            .Select(value => value.Id)
            .ToHashSet();

        var unknown = request.Scores.FirstOrDefault(score => !knownValueIds.Contains(score.ValueId));

        if (unknown is not null)
        {
            return Result.NotFound<Unit>($"No criterion value exists with id '{unknown.ValueId}'.");
        }

        var scores = request.Scores
            .GroupBy(score => score.ValueId)
            .ToDictionary(group => group.Key, group => group.Last().Score);

        await repository.UpdateScoresAsync(scores, cancellationToken);
        logger.CrossCuttingIssueScoresSaved(scores.Count);

        return Result.Success(Unit.Value);
    }
}
