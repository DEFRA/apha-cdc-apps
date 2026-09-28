using CDC.Web.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Infrastructure;

public interface IApiClient
{
    Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets enriched profile search results, with version history, from
    /// <c>GET /api/profile-search/search</c>.</summary>
    Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around CDC.Api. All business-logic/data calls from CDC.Web go through
// an interface like this rather than talking to the database directly.
public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<ApiHealthResponse>("/health", cancellationToken);

    public async Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString("/api/profile-search/search", new Dictionary<string, string?>
        {
            ["searchText"] = searchText,
            ["displayPublished"] = displayPublished.ToString(),
            ["displayDraft"] = displayDraft.ToString(),
            ["displayScenarios"] = displayScenarios.ToString()
        });

        var results = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileSearchResultDto>>(url, cancellationToken);

        return results ?? [];
    }
}

public sealed record ApiHealthResponse(string? Status, double UptimeSeconds, DateTime TimestampUtc);
