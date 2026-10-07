namespace CDC.Web.Models;

/// <summary>
/// A cross-cutting issue. Maps to <c>SpeciesPrioritisationCategory</c> in the legacy schema -
/// the issue itself holds no score; scores live on each criterion's values.
/// </summary>
public sealed record CrossCuttingIssueCategory(
    Guid Id,
    string Name,
    int SequenceNumber,
    IReadOnlyList<CrossCuttingIssueCriterion> Criteria);

/// <summary>A scoring criterion belonging to one cross-cutting issue.</summary>
public sealed record CrossCuttingIssueCriterion(
    Guid Id,
    string Name,
    int SequenceNumber,
    IReadOnlyList<CrossCuttingIssueCriterionValue> Values);

/// <summary>One selectable value of a criterion, carrying the score an administrator maintains.</summary>
/// <param name="Value">Display label, from the <c>ReferenceValue.LookupValue</c> lookup.</param>
/// <param name="Score">Integer score from 0 to 100 inclusive.</param>
public sealed record CrossCuttingIssueCriterionValue(
    Guid Id,
    string Value,
    int Score,
    int SequenceNumber);
