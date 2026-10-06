using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IUserAdminApiService"/>. The base address is configured on the underlying
/// <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is hardcoded here.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class UserAdminApiService(HttpClient httpClient) : IUserAdminApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainedUserDto>> GetGlobalUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await httpClient.GetFromJsonAsync<IReadOnlyList<MaintainedUserDto>>(
            "/api/user-admin/global-users", cancellationToken);

        return users ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MaintainedUserDto>> GetExternalUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await httpClient.GetFromJsonAsync<IReadOnlyList<MaintainedUserDto>>(
            "/api/user-admin/external-users", cancellationToken);

        return users ?? [];
    }

    /// <inheritdoc />
    public async Task<MaintainedUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/user-admin/users/{userId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MaintainedUserDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateReviewEmailSubscriptionResult> UpdateReviewEmailSubscriptionAsync(
        UpdateReviewEmailSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PutAsJsonAsync(
            $"/api/user-admin/users/{request.UserId}/review-email-subscription", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateReviewEmailSubscriptionResult(ReviewEmailSubscriptionOutcome.Success, null);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new UpdateReviewEmailSubscriptionResult(
                ReviewEmailSubscriptionOutcome.Conflict,
                "Another administrator has changed this user since the page was opened. Reload and try again."),
            HttpStatusCode.NotFound => new UpdateReviewEmailSubscriptionResult(
                ReviewEmailSubscriptionOutcome.NotFound,
                "This user no longer exists. They may have been removed."),
            HttpStatusCode.BadRequest => new UpdateReviewEmailSubscriptionResult(
                ReviewEmailSubscriptionOutcome.ValidationFailed,
                "Select whether this user should receive review emails."),
            _ => new UpdateReviewEmailSubscriptionResult(
                ReviewEmailSubscriptionOutcome.Error,
                "We could not save this change. Try again later.")
        };
    }
}
