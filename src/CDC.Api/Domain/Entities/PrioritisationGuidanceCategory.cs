namespace CDC.Api.Domain.Entities;

/// <summary>
/// A prioritisation category shown on the summary prioritisation report, with the criteria scored within it. Built from
/// the two result sets of the legacy <c>spgaPrioritisationGuidanceData</c> procedure.
/// </summary>
public sealed record PrioritisationGuidanceCategory
{
    /// <summary>Gets the category identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the heading the report prints for the category.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the category name.</summary>
    public required string CategoryName { get; init; }

    /// <summary>Gets the criteria, each listed once across all categories, in criterion-code order.</summary>
    public required IReadOnlyList<PrioritisationGuidanceCriterion> Criteria { get; init; }
}

/// <summary>A prioritisation criterion and the question whose guidance explains how to score it.</summary>
public sealed record PrioritisationGuidanceCriterion
{
    /// <summary>Gets the question whose guidance applies to the criterion.</summary>
    public required Guid ProfileQuestionId { get; init; }

    /// <summary>Gets the criterion code, for example <c>PMR01</c>.</summary>
    public required string CriterionCode { get; init; }

    /// <summary>Gets the criterion name.</summary>
    public required string CriterionName { get; init; }
}
