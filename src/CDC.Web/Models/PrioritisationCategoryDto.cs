namespace CDC.Web.Models;

/// <summary>
/// A prioritisation category and its criteria, as returned by
/// <c>GET /api/prioritisation-variables/categories</c> on CDC.Api.
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
