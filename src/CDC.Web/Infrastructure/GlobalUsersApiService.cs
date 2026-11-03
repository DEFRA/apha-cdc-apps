using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IGlobalUsersApiService"/>. The base address is configured on the underlying
/// <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is hardcoded here.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class GlobalUsersApiService(HttpClient httpClient) : IGlobalUsersApiService
{
    private const string BaseRoute = "/api/global-users";

    /// <inheritdoc />
    public async Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(
        int pageNumber, int pageSize, GlobalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(BaseRoute, new Dictionary<string, string?>
        {
            ["pageNumber"] = pageNumber.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["sortColumn"] = sortColumn.ToString(),
            ["sortDescending"] = sortDescending ? "true" : "false"
        });

        var result = await httpClient.GetFromJsonAsync<PagedResult<GlobalUserDto>>(url, cancellationToken);

        return result ?? new PagedResult<GlobalUserDto>
        {
            Items = [],
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = 0
        };
    }

    /// <inheritdoc />
    public async Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString($"{BaseRoute}/lookup", "userName", userName);

        var result = await httpClient.GetFromJsonAsync<GlobalUserLookupResult>(url, cancellationToken);

        return result ?? new GlobalUserLookupResult { Outcome = GlobalUserLookupOutcome.InvalidFormat };
    }

    /// <inheritdoc />
    public async Task<GlobalUserActionResult> AddAsync(SaveGlobalUserRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(BaseRoute, request, cancellationToken);

        return await ToResultAsync(response, "We could not add this user. Try again later.", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GlobalUserActionResult> UpdateAsync(Guid id, SaveGlobalUserRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", request, cancellationToken);

        return await ToResultAsync(response, "We could not save this change. Try again later.", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<GlobalUserActionResult> DeleteAsync(Guid id, byte[] lastUpdated, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString($"{BaseRoute}/{id}", "lastUpdated", Convert.ToBase64String(lastUpdated));

        using var response = await httpClient.DeleteAsync(url, cancellationToken);

        return await ToResultAsync(response, "We could not delete this global user. Try again later.", cancellationToken);
    }

    private static async Task<GlobalUserActionResult> ToResultAsync(
        HttpResponseMessage response, string genericError, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new GlobalUserActionResult { Outcome = GlobalUserActionOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new GlobalUserActionResult
            {
                Outcome = GlobalUserActionOutcome.Conflict,
                ErrorMessage = "Another user has changed this global user since the page was loaded. Reload and try again."
            },
            HttpStatusCode.BadRequest => new GlobalUserActionResult
            {
                Outcome = GlobalUserActionOutcome.ValidationFailed,
                ErrorMessage = await ReadProblemDetailAsync(response, cancellationToken)
            },
            _ => new GlobalUserActionResult { Outcome = GlobalUserActionOutcome.Error, ErrorMessage = genericError }
        };
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken);

            return problem?.Detail ?? "Correct the errors and try again.";
        }
        catch (System.Text.Json.JsonException)
        {
            return "Correct the errors and try again.";
        }
    }

    private sealed record ProblemDetailsBody(string? Detail);
}
