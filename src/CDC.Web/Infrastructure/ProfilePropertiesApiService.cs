using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IProfilePropertiesApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is
/// hardcoded here.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class ProfilePropertiesApiService(HttpClient httpClient) : IProfilePropertiesApiService
{
    /// <inheritdoc />
    public async Task<NewProfileDefaultsDto?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString("/api/profiles/defaults", new Dictionary<string, string?>
        {
            ["cloneProfileVersionId"] = cloneProfileVersionId.ToString(),
            ["isWhatIfScenario"] = isWhatIfScenario.ToString()
        });

        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<NewProfileDefaultsDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CreateProfileResult> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/profiles", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var created = await response.Content.ReadFromJsonAsync<CreateProfileResultDto>(cancellationToken);

            return new CreateProfileResult(SaveProfileOutcome.Success, created?.NewProfileId ?? request.Id, null);
        }

        var (outcome, message) = await ReadFailureAsync(response, "The profile could not be created. Please try again.", cancellationToken);

        return new CreateProfileResult(outcome, null, message);
    }

    /// <inheritdoc />
    public async Task<UpdateProfileAttributesResult> UpdateProfileAttributesAsync(
        Guid profileId,
        UpdateProfileAttributesRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"/api/profiles/{profileId}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateProfileAttributesResult(SaveProfileOutcome.Success, null);
        }

        var (outcome, message) = await ReadFailureAsync(response, "The profile could not be saved. Please try again.", cancellationToken);

        return new UpdateProfileAttributesResult(outcome, message);
    }

    private static async Task<(SaveProfileOutcome Outcome, string Message)> ReadFailureAsync(
        HttpResponseMessage response,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            return (SaveProfileOutcome.Conflict, problem?.Detail ?? "This profile has been edited by another user. Reload the page and try again.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var message = problem?.Errors.Count > 0
                ? string.Join(" ", problem.Errors.SelectMany(error => error.Value))
                : fallbackMessage;

            return (SaveProfileOutcome.ValidationFailed, message);
        }

        return (SaveProfileOutcome.Error, fallbackMessage);
    }
}
