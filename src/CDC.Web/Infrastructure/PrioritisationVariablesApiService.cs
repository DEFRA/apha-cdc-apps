using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IPrioritisationVariablesApiService"/>. The base address is configured on
/// the underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>).
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class PrioritisationVariablesApiService(HttpClient httpClient) : IPrioritisationVariablesApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PrioritisationCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await httpClient.GetFromJsonAsync<IReadOnlyList<PrioritisationCategoryDto>>(
            "/api/prioritisation-variables/categories", cancellationToken);

        return categories ?? [];
    }

    /// <inheritdoc />
    public async Task UpdateCriterionAsync(
        Guid criterionId,
        int weight,
        IReadOnlyList<CriterionValueScore> valueScores,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/api/prioritisation-variables/criteria/{criterionId}",
            new
            {
                Weight = weight,
                Name = name,
                ValueScores = valueScores.Select(valueScore => new { valueScore.ValueId, valueScore.Score })
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<PrioritisationRankingRangeDto> GetRankingRangeAsync(CancellationToken cancellationToken = default)
    {
        var rankingRange = await httpClient.GetFromJsonAsync<PrioritisationRankingRangeDto>(
            "/api/prioritisation-variables/ranking-range", cancellationToken);

        return rankingRange ?? new PrioritisationRankingRangeDto { LowerBound = 0, UpperBound = 90 };
    }

    /// <inheritdoc />
    public async Task<PrioritisationRankingRangeDto> UpdateRankingRangeAsync(
        PrioritisationRankingRangeDto rankingRange,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            "/api/prioritisation-variables/ranking-range",
            rankingRange,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new RankingRangeConflictException(
                "Someone else has updated the prioritisation variables. Reload the page and try again.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PrioritisationRankingRangeDto>(cancellationToken)
            ?? rankingRange;
    }

    /// <inheritdoc />
    public async Task PrioritiseDraftProfilesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("/api/prioritisation-variables/draft-prioritisation", null, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task RecalculateScoresAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("/api/prioritisation-variables/score-recalculation", null, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<PrioritisationScoreExport?> ExportCategoryScoreDataAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/prioritisation-variables/categories/{categoryId}/score-data", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadExportAsync(response, "ScoreData.csv", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PrioritisationScoreExport> ExportNormalisedScoresAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/prioritisation-variables/normalised-scores", cancellationToken);

        return await ReadExportAsync(response, "NormalisedScoreSummary.csv", cancellationToken);
    }

    private static async Task<PrioritisationScoreExport> ReadExportAsync(HttpResponseMessage response, string defaultFileName, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');

        return new PrioritisationScoreExport(content, string.IsNullOrWhiteSpace(fileName) ? defaultFileName : fileName);
    }
}
