using CDC.Auth.Entra.Options;
using CDC.Common.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace CDC.Auth.Entra.TokenRefresh;

/// <summary>Exchanges an Entra ID refresh_token for a new token set ahead of access token expiry.</summary>
public sealed class EntraTokenRefreshService : OidcTokenRefreshService, IEntraTokenRefreshService
{
    /// <summary>Name of the named <see cref="IHttpClientFactory"/> client used for refresh_token requests.</summary>
    public const string HttpClientName = "EntraTokenRefresh";

    /// <summary>Creates the service with its HTTP client factory, OIDC options, and Entra options dependencies.</summary>
    public EntraTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<EntraOptions> entraOptions)
        : base(
            httpClientFactory,
            oidcOptions,
            EntraAuthenticationDefaults.AuthenticationScheme,
            HttpClientName,
            entraOptions.Value.ClientId,
            entraOptions.Value.ClientSecret,
            entraOptions.Value.Scopes)
    {
    }
}
