namespace CDC.Web.Infrastructure;

public interface IApiClient
{
    Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around CDC.Api. All business-logic/data calls from CDC.Web go through
// an interface like this rather than talking to the database directly.
public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<ApiHealthResponse>("/health", cancellationToken);
}

public sealed record ApiHealthResponse(string? Status, double UptimeSeconds, DateTime TimestampUtc);
