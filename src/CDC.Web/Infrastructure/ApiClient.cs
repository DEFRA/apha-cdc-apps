using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>Gets a profile's attributes from <c>GET /api/profiles/{profileId}/attributes</c>.</summary>
    /// <returns>The profile's attributes, or <see langword="null"/> when no such profile exists.</returns>
    Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Updates a profile's title via <c>PUT /api/profiles/{profileId}</c>, leaving every
    /// other attribute unchanged.</summary>
    Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the details shown on the "Manage profile" page from
    /// <c>GET /api/profiles/{profileId}/manage</c>.</summary>
    Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
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

    public async Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/profiles/{profileId}/attributes", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProfileAttributesDto>(cancellationToken);
    }

    public async Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateProfileTitleRequest { Id = profileId, Title = title, LastUpdated = lastUpdated };
        var response = await httpClient.PutAsJsonAsync($"/api/profiles/{profileId}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            return new UpdateProfileTitleResult(
                UpdateProfileTitleOutcome.Conflict,
                problem?.Detail ?? "This profile has been edited by another user. Reload the page and try again.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var message = problem?.Errors.Count > 0
                ? string.Join(" ", problem.Errors.SelectMany(error => error.Value))
                : "The profile title could not be saved.";

            return new UpdateProfileTitleResult(UpdateProfileTitleOutcome.ValidationFailed, message);
        }

        return new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Error, "The profile title could not be saved. Please try again.");
    }

    public async Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/profiles/{profileId}/manage", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ManageProfileViewModel>(cancellationToken);
    }
}

public sealed record ApiHealthResponse(string? Status, double UptimeSeconds, DateTime TimestampUtc);

/// <summary>Outcome of <see cref="IApiClient.UpdateProfileTitleAsync"/>.</summary>
public enum UpdateProfileTitleOutcome
{
    Success,
    ValidationFailed,
    Conflict,
    Error
}

/// <summary>Result of attempting to update a profile's title.</summary>
public sealed record UpdateProfileTitleResult(UpdateProfileTitleOutcome Outcome, string? ErrorMessage);
