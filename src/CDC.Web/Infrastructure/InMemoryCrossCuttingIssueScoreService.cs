using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// In-memory <see cref="ICrossCuttingIssueScoreService"/>, registered as a singleton so edits
/// made by one administrator are visible to the next request. Placeholder until scores are
/// stored in CDC.Api / the database.
/// </summary>
public sealed class InMemoryCrossCuttingIssueScoreService : ICrossCuttingIssueScoreService
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, (string Name, decimal Weight, int Score)> _issues = new()
    {
        [1] = ("Trade sensitivity", 1.2m, 3),
        [2] = ("Public health significance", 1.5m, 4),
        [3] = ("Zoonotic potential", 1.5m, 3),
        [4] = ("International reporting obligations", 1.0m, 2),
        [5] = ("Reputational risk", 0.8m, 2)
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<CrossCuttingIssueScoreDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<CrossCuttingIssueScoreDto>>(Snapshot());
        }
    }

    /// <inheritdoc />
    public Task<CrossCuttingIssueScoreDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(Snapshot().FirstOrDefault(issue => issue.Id == id));
        }
    }

    /// <inheritdoc />
    public Task<CrossCuttingIssueRecalculationResultDto> UpdateScoreAsync(int id, int score, CancellationToken cancellationToken = default)
    {
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be between 1 and 5.");
        }

        lock (_sync)
        {
            if (!_issues.TryGetValue(id, out var issue))
            {
                throw new KeyNotFoundException($"Cross-cutting issue {id} was not found.");
            }

            _issues[id] = (issue.Name, issue.Weight, score);

            var issues = Snapshot();
            var overallScore = issues.Count == 0 ? 0m : Math.Round(issues.Average(i => i.WeightedScore), 1);

            return Task.FromResult(new CrossCuttingIssueRecalculationResultDto(issues, overallScore, DateTimeOffset.UtcNow));
        }
    }

    private List<CrossCuttingIssueScoreDto> Snapshot() =>
        [.. _issues
            .Select(pair => new CrossCuttingIssueScoreDto(pair.Key, pair.Value.Name, pair.Value.Score, Math.Round(pair.Value.Score * pair.Value.Weight, 1)))
            .OrderBy(issue => issue.Name)];
}
