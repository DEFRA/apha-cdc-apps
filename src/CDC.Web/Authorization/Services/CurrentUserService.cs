using CDC.Web.Authorization.Models;
using Microsoft.Extensions.Options;

namespace CDC.Web.Authorization.Services;

/// <summary>
/// Temporary <see cref="ICurrentUserService"/> that simulates an authenticated user from the
/// <c>AuthorizationSettings</c> configuration section, until real authentication (Entra ID SAML)
/// is integrated. Reads configuration only - never a hardcoded user id or role.
/// </summary>
/// <param name="options">The bound <c>AuthorizationSettings</c> configuration section.</param>
public sealed class CurrentUserService(IOptions<AuthorizationSettings> options) : ICurrentUserService
{
    /// <inheritdoc />
    public string UserId => options.Value.PlaceholderUserId;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles => options.Value.PlaceholderRoles;
}
