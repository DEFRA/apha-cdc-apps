namespace CDC.Api.Features.CrossCuttingIssueScores.Dtos;

/// <summary>A cross-cutting issue (<c>SpeciesPrioritisationCategory</c>) with its criteria.</summary>
public sealed record CrossCuttingIssueCategoryDto(Guid Id, string Name, int SequenceNumber, IReadOnlyList<CrossCuttingIssueCriterionDto> Criteria);

/// <summary>A scoring criterion belonging to one cross-cutting issue.</summary>
public sealed record CrossCuttingIssueCriterionDto(Guid Id, string Name, int SequenceNumber, IReadOnlyList<CrossCuttingIssueCriterionValueDto> Values);

/// <summary>One selectable value of a criterion and its maintained score.</summary>
public sealed record CrossCuttingIssueCriterionValueDto(Guid Id, string Value, int Score, int SequenceNumber);

/// <summary>A new score for one criterion value.</summary>
public sealed record CrossCuttingIssueValueScoreDto(Guid ValueId, int Score);

/// <summary>The changed criterion-value scores to commit together.</summary>
public sealed record UpdateCrossCuttingIssueScoresRequestDto(IReadOnlyList<CrossCuttingIssueValueScoreDto> Scores);
