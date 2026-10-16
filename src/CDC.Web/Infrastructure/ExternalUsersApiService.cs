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

    /// <inheritdoc />
    public async Task<PagedResult<MaintainedExternalUserDto>> GetExternalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(BaseRoute, new Dictionary<string, string?>
        {
            ["pageNumber"] = pageNumber.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
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

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken);

            return problem?.Detail ?? "Please correct the highlighted fields.";
        }
        catch (System.Text.Json.JsonException)
        {
            return "Please correct the highlighted fields.";
        }
    }

    private sealed record ProblemDetailsBody(string? Detail);
}
