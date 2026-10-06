using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IProfileContributorsApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is
/// hardcoded here.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class ProfileContributorsApiService(HttpClient httpClient) : IProfileContributorsApiService
{
    /// <inheritdoc />
    public async Task<PagedResult<ContributorDto>> GetProfileContributorsAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString($"/api/profiles/{profileId}/contributors", new Dictionary<string, string?>
        {
            ["pageNumber"] = pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        var result = await httpClient.GetFromJsonAsync<PagedResult<ContributorDto>>(url, cancellationToken);

        return result ?? new PagedResult<ContributorDto>
        {
            Items = [],
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = 0
        };
    }

    /// <inheritdoc />
    public async Task<ContributorEditDto?> GetContributorForEditAsync(
        Guid profileId,
        Guid contributorId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"/api/profiles/{profileId}/contributors/{contributorId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ContributorEditDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileUserRoleDto>> GetProfileUserRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileUserRoleDto>>("/api/profile-user-roles", cancellationToken);

        return roles ?? [];
    }

    /// <inheritdoc />
    public async Task<UpdateContributorResult> UpdateContributorAsync(
        Guid profileId,
        Guid contributorId,
        UpdateContributorRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"/api/profiles/{profileId}/contributors/{contributorId}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateContributorResult { Outcome = ContributorUpdateOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new UpdateContributorResult
            {
                Outcome = ContributorUpdateOutcome.Conflict,
                ErrorMessage = "Another user has changed this contributor since it was opened. Reload and try again."
            },
            HttpStatusCode.BadRequest => new UpdateContributorResult
            {
                Outcome = ContributorUpdateOutcome.ValidationFailed,
                ErrorMessage = await ReadProblemDetailAsync(response, cancellationToken)
            },
            _ => new UpdateContributorResult
            {
                Outcome = ContributorUpdateOutcome.Error,
                ErrorMessage = "We could not save this change. Try again later."
            }
        };
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

    /// <inheritdoc />
    public async Task<UserVerificationResultDto> VerifyContributorUsernameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(
            "/api/profile-contributors/verify-username", "userName", userName);

        var result = await httpClient.GetFromJsonAsync<UserVerificationResultDto>(url, cancellationToken);

        return result ?? new UserVerificationResultDto { Outcome = UserVerificationOutcome.InvalidFormat };
    }

    /// <inheritdoc />
    public async Task<AddContributorResult> AddContributorAsync(
        Guid profileId, AddContributorRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"/api/profiles/{profileId}/contributors", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new AddContributorResult { Outcome = ContributorAddOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new AddContributorResult
            {
                Outcome = ContributorAddOutcome.Conflict,
                ErrorMessage = "Another user has changed this user since it was looked up. Reload and try again."
            },
            HttpStatusCode.BadRequest => new AddContributorResult
            {
                Outcome = ContributorAddOutcome.ValidationFailed,
                ErrorMessage = await ReadProblemDetailAsync(response, cancellationToken)
            },
            _ => new AddContributorResult
            {
                Outcome = ContributorAddOutcome.Error,
                ErrorMessage = "We could not add this contributor. Try again later."
            }
        };
    }

    /// <inheritdoc />
    public async Task<DeleteContributorResult> DeleteContributorAsync(
        Guid profileId, Guid contributorId, byte[] lastUpdated, CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(
            $"/api/profiles/{profileId}/contributors/{contributorId}", "lastUpdated", Convert.ToBase64String(lastUpdated));

        using var response = await httpClient.DeleteAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new DeleteContributorResult { Outcome = ContributorDeleteOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new DeleteContributorResult
            {
                Outcome = ContributorDeleteOutcome.Conflict,
                ErrorMessage = "Another user has changed this contributor since the page was loaded. Reload and try again."
            },
            _ => new DeleteContributorResult
            {
                Outcome = ContributorDeleteOutcome.Error,
                ErrorMessage = "We could not remove this contributor. Try again later."
            }
        };
    }

    private sealed record ProblemDetailsBody
    {
        public string? Detail { get; init; }
    }
}
