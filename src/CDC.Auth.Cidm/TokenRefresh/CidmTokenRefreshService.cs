using CDC.Auth.Cidm.Options;
using CDC.Common.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace CDC.Auth.Cidm.TokenRefresh;

/// <summary>
/// Exchanges a CIDM refresh_token for a new token set ahead of access token expiry, per the "Refresh
/// expired token(s)" section of the onboarding guide.
/// </summary>
public sealed class CidmTokenRefreshService : OidcTokenRefreshService, ICidmTokenRefreshService
{
    /// <summary>Name of the named <see cref="IHttpClientFactory"/> client used for refresh_token requests.</summary>
    public const string HttpClientName = "CidmTokenRefresh";

    /// <summary>Creates the service with its HTTP client factory, OIDC options, and CIDM options dependencies.</summary>
    public CidmTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<CidmOptions> cidmOptions)
        : base(
            httpClientFactory,
            oidcOptions,
            CidmAuthenticationDefaults.AuthenticationScheme,
            HttpClientName,
            cidmOptions.Value.ClientId,
            cidmOptions.Value.ClientSecret,
            cidmOptions.Value.AllScopes)
    {
    }
}
