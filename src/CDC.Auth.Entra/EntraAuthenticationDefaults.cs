namespace CDC.Auth.Entra;

/// <summary>Well-known constants for the Entra ID authentication schemes registered by <see cref="EntraAuthenticationExtensions"/>.</summary>
public static class EntraAuthenticationDefaults
{
    /// <summary>Scheme name used for the OpenID Connect handler registered by <see cref="EntraAuthenticationExtensions"/>.</summary>
    public const string AuthenticationScheme = "entra";

    /// <summary>
    /// Scheme name used for the internal-user session cookie. Distinct from the external (CIDM) cookie
    /// scheme so the two identities never collide in the same ASP.NET Core authentication options -
    /// both exist side by side in this app, selected per-controller/action via
    /// <c>[Authorize(AuthenticationSchemes = ...)]</c> or the app's fallback authorization policy.
    /// </summary>
    public const string CookieAuthenticationScheme = "Cookies.Entra";
}
