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
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/api/prioritisation-variables/criteria/{criterionId}",
            new
            {
                Weight = weight,
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

        return rankingRange ?? new PrioritisationRankingRangeDto();
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
}
