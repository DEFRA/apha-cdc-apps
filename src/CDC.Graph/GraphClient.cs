using System.Net;
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
    private const string SelectedProperties = "id,onPremisesSamAccountName,mail,givenName,surname";

    /// <inheritdoc />
    public async Task<GraphUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(samAccountName);

        // OData escapes a single quote by doubling it, so the value cannot break out of the filter literal.
        var name = samAccountName.Trim().Replace("'", "''", StringComparison.Ordinal);
        var filter = $"onPremisesSamAccountName eq '{name}' or employeeId eq '{name}'";

        // $top=2 is enough to detect an ambiguous match; $count and ConsistencyLevel enable Graph's advanced query.
        var url = QueryHelpers.AddQueryString("users", new Dictionary<string, string?>
        {
            ["$filter"] = filter,
            ["$select"] = SelectedProperties,
            ["$top"] = "2",
            ["$count"] = "true"
        });

        var body = await GetAsync<UsersResponse>(url, advancedQuery: true, cancellationToken);

        return body?.Value is [var match] ? ToUser(match) : null;
    }

    /// <inheritdoc />
    public async Task<string?> FindEmployeeIdAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var body = await GetAsync<UserResponse>($"users/{objectId:D}?$select=employeeId", advancedQuery: false, cancellationToken);

        return string.IsNullOrWhiteSpace(body?.EmployeeId) ? null : body.EmployeeId.Trim();
    }

    /// <summary>Sends a GET and reads the JSON body; a missing resource (404) yields <see langword="null"/>.</summary>
    private async Task<TBody?> GetAsync<TBody>(string url, bool advancedQuery, CancellationToken cancellationToken)
        where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (advancedQuery)
        {
            request.Headers.Add("ConsistencyLevel", "eventual");
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GraphException($"Microsoft Graph returned status {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<TBody>(cancellationToken);
        }
        catch (Exception exception) when (exception is not GraphException && !cancellationToken.IsCancellationRequested)
        {
            throw new GraphException("The Microsoft Graph lookup failed.", exception);
        }
    }

    private static GraphUser? ToUser(UserResponse user)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return null;
        }

        var graphUser = new GraphUser(user.Id, user.OnPremisesSamAccountName, user.Mail, user.GivenName, user.Surname);

        return graphUser.DisplayName.Length == 0 ? null : graphUser;
    }

    private sealed record UsersResponse(List<UserResponse>? Value);

    private sealed record UserResponse(string? Id, string? OnPremisesSamAccountName, string? Mail, string? GivenName, string? Surname, string? EmployeeId = null);
}
