namespace CDC.Common.Auth;

/// <summary>
/// Common shape for the outcome of resolving a validated identity-provider principal (CIDM, Entra
/// ID, ...) onto the consuming application's own user record, so <see cref="OpenIdConnectEventHelpers"/>
/// can operate on any provider's resolution type without depending on it directly.
/// </summary>
public interface IIdentityResolution
{
    /// <summary>Gets a value indicating whether sign-in is permitted.</summary>
    bool IsAllowed { get; }

    /// <summary>Gets the local path to redirect to instead, when <see cref="IsAllowed"/> is <see langword="false"/>.</summary>
    string? DenialRedirectPath { get; }

    /// <summary>Gets additional claims to add to the principal, when <see cref="IsAllowed"/> is <see langword="true"/>.</summary>
    IReadOnlyDictionary<string, string>? Claims { get; }
}
