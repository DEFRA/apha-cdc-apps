using CDC.Web.Authorization.Constants;

namespace CDC.Web.Authorization.Services;

/// <summary>
/// Temporary <see cref="ICurrentUserService"/> that simulates an authenticated profile editor with
/// hardcoded values until real authentication (Entra ID) supplies the signed-in user.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    /// <summary>The placeholder user id; never a real identity.</summary>
    public const string PlaceholderUserId = "REPLACE_WITH_REAL_AUTHENTICATED_USER";

    /// <inheritdoc />
    public string UserId => PlaceholderUserId;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles { get; } = [AuthorizationRoles.ProfileEditor];
}
