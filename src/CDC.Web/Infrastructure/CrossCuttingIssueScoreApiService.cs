using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// <see cref="ICrossCuttingIssueScoreService"/> backed by CDC.Api. The base address is configured on
/// the underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>).
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class CrossCuttingIssueScoreApiService(HttpClient httpClient) : ICrossCuttingIssueScoreService
{
    private const string Path = "/api/cross-cutting-issue-scores";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrossCuttingIssueCategory>> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var categories = await httpClient.GetFromJsonAsync<IReadOnlyList<CrossCuttingIssueCategory>>(Path, cancellationToken);

        return categories ?? [];
    }

    /// <inheritdoc />
    public async Task SaveAsync(IReadOnlyDictionary<Guid, int> scores, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scores);

        using var response = await httpClient.PutAsJsonAsync(
            Path,
            new { Scores = scores.Select(score => new { ValueId = score.Key, Score = score.Value }) },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
