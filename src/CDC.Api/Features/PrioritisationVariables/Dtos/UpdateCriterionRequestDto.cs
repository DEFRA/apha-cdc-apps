namespace CDC.Api.Features.PrioritisationVariables.Dtos;

/// <summary>
/// One criterion value's new score, within <see cref="UpdateCriterionRequestDto"/>.
/// </summary>
public sealed record UpdateCriterionValueScoreRequestDto
{
    /// <summary>Gets the criterion value identifier.</summary>
    public Guid ValueId { get; init; }

    /// <summary>Gets the new score.</summary>
    public int Score { get; init; }
}

/// <summary>
/// Request body for updating a prioritisation criterion's weighting and value scores.
/// </summary>
public sealed record UpdateCriterionRequestDto
{
    /// <summary>Gets the new weighting.</summary>
    public required int Weight { get; init; }

    /// <summary>Gets the new score for every value belonging to the criterion.</summary>
    public IReadOnlyList<UpdateCriterionValueScoreRequestDto> ValueScores { get; init; } = [];
}
