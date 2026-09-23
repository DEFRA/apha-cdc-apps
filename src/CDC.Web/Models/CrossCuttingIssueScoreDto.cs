namespace CDC.Web.Models;

/// <summary>A single cross-cutting issue and its current prioritisation score.</summary>
/// <param name="Id">Stable identifier used to select the issue from the dropdown.</param>
/// <param name="Name">Display name of the cross-cutting issue.</param>
/// <param name="Score">Current score, from 1 (lowest) to 5 (highest).</param>
/// <param name="WeightedScore">Score with the issue's weighting applied, refreshed on every recalculation.</param>
public sealed record CrossCuttingIssueScoreDto(int Id, string Name, int Score, decimal WeightedScore);

/// <summary>The outcome of recalculating every cross-cutting issue score after one is updated.</summary>
/// <param name="Issues">Every issue with its refreshed <see cref="CrossCuttingIssueScoreDto.WeightedScore"/>.</param>
/// <param name="OverallScore">The combined score across all issues, used on species prioritisation.</param>
/// <param name="RecalculatedUtc">When the recalculation ran.</param>
public sealed record CrossCuttingIssueRecalculationResultDto(
    IReadOnlyList<CrossCuttingIssueScoreDto> Issues,
    decimal OverallScore,
    DateTimeOffset RecalculatedUtc);
