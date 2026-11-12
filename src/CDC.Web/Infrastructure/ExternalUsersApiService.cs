using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IExternalUsersApiService"/>. The base address is configured on the underlying
/// <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>).
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class ExternalUsersApiService(HttpClient httpClient) : IExternalUsersApiService
{
    private const string BaseRoute = "/api/external-users";
    private const string SendInstructionsFailed = "We could not send the login instructions. Try again later.";

    /// <inheritdoc />
    public async Task<PagedResult<MaintainedExternalUserDto>> GetExternalUsersAsync(
        int pageNumber, int pageSize, ExternalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(BaseRoute, new Dictionary<string, string?>
        {
            ["pageNumber"] = pageNumber.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["sortColumn"] = sortColumn.ToString(),
            ["sortDescending"] = sortDescending ? "true" : "false"
        });

        var result = await httpClient.GetFromJsonAsync<PagedResult<MaintainedExternalUserDto>>(url, cancellationToken);

        return result ?? new PagedResult<MaintainedExternalUserDto>
        {
            Items = [],
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = 0
        };
    }

    /// <inheritdoc />
    public async Task<ExternalUserAccessResult> PreRegisterExternalUserAsync(PreRegisterExternalUserContract request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ExternalUserAccessResult(true, null);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new ExternalUserAccessResult(false, "A user with this email address already exists."),
            HttpStatusCode.BadRequest => new ExternalUserAccessResult(false, await ReadProblemDetailAsync(response, cancellationToken)),
            _ => new ExternalUserAccessResult(false, "We could not register this user. Try again later.")
        };
    }

    /// <inheritdoc />
    public async Task<bool> SetReviewEmailSubscriptionAsync(Guid id, bool subscribed, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"{BaseRoute}/{id}/review-email-subscription",
            new SetReviewEmailSubscriptionRequest { SubscribedToReviewEmails = subscribed },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<ExternalUserProfileAccessContract?> GetProfileAccessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BaseRoute}/{id}/profile-access", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ExternalUserProfileAccessContract>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ExternalUserAccessResult> SaveProfileAccessAsync(
        Guid id, Guid profileId, SaveExternalUserProfileAccessContract request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}/profile-access/{profileId}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ExternalUserAccessResult(true, null);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new ExternalUserAccessResult(false, "Another user has changed this access since it was opened. Reload and try again."),
            HttpStatusCode.BadRequest => new ExternalUserAccessResult(false, await ReadProblemDetailAsync(response, cancellationToken)),
            _ => new ExternalUserAccessResult(false, "We could not save this change. Try again later.")
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfileAccessAsync(Guid id, Guid profileId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{BaseRoute}/{id}/profile-access/{profileId}", cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<ExternalUserAccessResult> SendLoginInstructionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"{BaseRoute}/{id}/login-instructions", content: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ExternalUserAccessResult(true, null);
        }

        // The API's messages for these two statuses are written for the user; anything else is reported generically.
        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => new ExternalUserAccessResult(false, "This user could not be found. Reload the page and try again."),
            HttpStatusCode.BadRequest or HttpStatusCode.Conflict => new ExternalUserAccessResult(
                false, await ReadProblemDetailAsync(response, cancellationToken, SendInstructionsFailed)),
            _ => new ExternalUserAccessResult(false, SendInstructionsFailed)
        };
    }

    private static async Task<string> ReadProblemDetailAsync(
        HttpResponseMessage response, CancellationToken cancellationToken, string fallback = "Please correct the highlighted fields.")
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken);

            return problem?.Detail ?? fallback;
        }
        catch (System.Text.Json.JsonException)
        {
            return fallback;
        }
    }

    private sealed record ProblemDetailsBody(string? Detail);
}
