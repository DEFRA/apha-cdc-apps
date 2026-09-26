namespace CDC.Auth.Cidm.Options;

/// <summary>Configuration bound from the <c>Cidm</c> section, controlling DEFRA CIDM OpenID Connect sign-in.</summary>
public sealed class CidmOptions
{
    /// <summary>The configuration section name this type binds to ("Cidm").</summary>
    public const string SectionName = "Cidm";

    /// <summary>Per-environment DEFRA IdP Hub base address, e.g. https://your-account.cpdev.cui.defra.gov.uk/idphub/b2c.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Per-environment B2C policy name, e.g. b2c_1a_cui_cpdev_signupsignin.</summary>
    public string Policy { get; set; } = string.Empty;

    /// <summary>The app registration's client ID, issued by DEFRA when the app is onboarded to CIDM.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The app registration's client secret. Never stored in source control or appsettings.json.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>DEFRA-specific parameter required on the /authorize request; not part of the OIDC spec.</summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>OAuth2 <c>response_type</c> requested on the /authorize call. CIDM requires the authorization code flow.</summary>
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// OpenID Connect <c>response_mode</c>. DEFRA recommends <c>form_post</c>, which requires
    /// <c>SameSite=None</c>+<c>Secure</c> cookies (HTTPS-only); <see cref="CidmAuthenticationExtensions"/> falls
    /// back to <c>query</c> automatically in the Development environment so local HTTP testing still works.
    /// </summary>
    public string ResponseMode { get; set; } = "form_post";

    /// <summary>OAuth2 scopes requested. Must include "openid"; see also <see cref="AllScopes"/>.</summary>
    public IReadOnlyList<string> Scopes { get; set; } = ["openid", "offline_access"];

    /// <summary>Path the OIDC handler listens on for the identity provider's authorization code callback.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>Path the OIDC handler listens on for the identity provider's post-logout redirect.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-oidc";

    /// <summary>How long before access token expiry a background refresh is attempted.</summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Composed rather than stored directly, so environments only need to supply <see cref="Address"/> and
    /// <see cref="Policy"/> - see the CIDM onboarding guide's "OpenID Connect configuration" section.
    /// </summary>
    public string MetadataAddress => $"{Address.TrimEnd('/')}/{Policy}/.well-known/openid-configuration";

    /// <summary>
    /// The client_id must also be sent as a scope entry (per the CIDM guide) to request an access token, in
    /// addition to the standard scopes.
    /// </summary>
    public IEnumerable<string> AllScopes => Scopes.Append(ClientId);
}
