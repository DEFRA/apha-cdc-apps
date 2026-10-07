using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A prioritisation category and its criteria, as returned by the <c>PrioritisationCategory</c>
/// and <c>PrioritisationCriterion</c> result sets of <c>spgaPrioritisationVariables</c>. Mirrors
/// the legacy <c>ProfilePrioritisationCategory</c> business object.
/// </summary>
public sealed record PrioritisationCategory : BaseEntity
{
    /// <summary>Gets the category name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the criteria belonging to this category, in code order.</summary>
    public required IReadOnlyList<PrioritisationCriterion> Criteria { get; init; }
}
