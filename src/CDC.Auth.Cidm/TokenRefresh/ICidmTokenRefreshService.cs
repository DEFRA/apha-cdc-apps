namespace CDC.Auth.Cidm.TokenRefresh;

/// <summary>Outcome of a CIDM refresh_token exchange.</summary>
/// <param name="Succeeded">Whether the exchange succeeded.</param>
/// <param name="AccessToken">The new access token, if successful.</param>
/// <param name="IdToken">The new ID token, if successful.</param>
/// <param name="RefreshToken">The new refresh token, if successful.</param>
/// <param name="ExpiresAt">When the new access token expires, if successful.</param>
public sealed record CidmTokenRefreshResult(bool Succeeded, string? AccessToken, string? IdToken, string? RefreshToken, DateTimeOffset? ExpiresAt)
{
    /// <summary>A shared, singleton failure result.</summary>
    public static CidmTokenRefreshResult Failed { get; } = new(false, null, null, null, null);
}

/// <summary>Exchanges a CIDM refresh_token for a new token set.</summary>
public interface ICidmTokenRefreshService
{
    /// <summary>Attempts to refresh a CIDM token set.</summary>
    /// <param name="refreshToken">The refresh_token from the current session.</param>
    /// <param name="redirectUri">
    /// Must exactly match the redirect_uri used on the original /authorize request - the caller builds this
    /// from the current request, since this service has no HTTP context of its own.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the outbound HTTP request.</param>
    Task<CidmTokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default);
}
