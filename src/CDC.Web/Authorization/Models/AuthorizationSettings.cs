namespace CDC.Web.Authorization.Models;

/// <summary>
/// Temporary configuration for simulating an authenticated user until real authentication
/// (Entra ID SAML for internal users) is integrated. Bound from the <c>AuthorizationSettings</c>
/// configuration section - see <c>appsettings.Development.json</c>. Must never be populated in a
/// real (non-Development) environment: <see cref="Services.CurrentUserService"/> fails closed
/// (no roles granted) when this section is absent.
/// </summary>
public sealed class AuthorizationSettings
{
    /// <summary>The configuration section name this binds from.</summary>
    public const string SectionName = "AuthorizationSettings";

    /// <summary>Gets or sets the placeholder user id. Never a real identity - replaced by the
    /// authenticated user's claim once real authentication is integrated.</summary>
    public string PlaceholderUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the role(s) the placeholder user simulates.</summary>
    public IReadOnlyList<string> PlaceholderRoles { get; set; } = [];
}
