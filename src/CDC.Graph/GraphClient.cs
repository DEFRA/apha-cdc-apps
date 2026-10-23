using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Graph;

/// <summary>
/// Default <see cref="IGraphClient"/>. The bearer token is added by <c>GraphAuthorizationHandler</c> on the
/// underlying <see cref="HttpClient"/>, so nothing here handles credentials.
/// </summary>
/// <param name="httpClient">Typed client whose base address is the Graph v1.0 endpoint.</param>
internal sealed class GraphClient(HttpClient httpClient) : IGraphClient
{
    private const string SelectedProperties = "id,displayName,companyName,department";

    /// <inheritdoc />
    public async Task<GraphUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(samAccountName);

        // OData escapes a single quote by doubling it, so the value cannot break out of the filter literal.
        var filter = $"onPremisesSamAccountName eq '{samAccountName.Trim().Replace("'", "''", StringComparison.Ordinal)}'";

        // $top=2 is enough to detect an ambiguous match; $count and ConsistencyLevel enable Graph's advanced query.
        var url = QueryHelpers.AddQueryString("users", new Dictionary<string, string?>
        {
            ["$filter"] = filter,
            ["$select"] = SelectedProperties,
            ["$top"] = "2",
            ["$count"] = "true"
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("ConsistencyLevel", "eventual");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new GraphException($"Microsoft Graph returned status {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<UsersResponse>(cancellationToken);

            return body?.Value is [var match] ? ToUser(match) : null;
        }
        catch (Exception exception) when (exception is not GraphException && !cancellationToken.IsCancellationRequested)
        {
            throw new GraphException("The Microsoft Graph lookup failed.", exception);
        }
    }

    private static GraphUser? ToUser(UserResponse user)
    {
        if (string.IsNullOrWhiteSpace(user.Id) || string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return null;
        }

        var organisation = !string.IsNullOrWhiteSpace(user.CompanyName) ? user.CompanyName : user.Department;

        return new GraphUser(user.Id, user.DisplayName, string.IsNullOrWhiteSpace(organisation) ? null : organisation);
    }

    private sealed record UsersResponse(List<UserResponse>? Value);

    private sealed record UserResponse(string? Id, string? DisplayName, string? CompanyName, string? Department);
}
