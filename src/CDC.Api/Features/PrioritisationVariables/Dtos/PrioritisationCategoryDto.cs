namespace CDC.Api.Features.PrioritisationVariables.Dtos;

/// <summary>
/// A prioritisation category and its criteria.
/// </summary>
public sealed record PrioritisationCategoryDto
{
    /// <summary>Gets the category identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the category name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the criteria belonging to this category, in code order.</summary>
    public IReadOnlyList<PrioritisationCriterionDto> Criteria { get; init; } = [];
}
