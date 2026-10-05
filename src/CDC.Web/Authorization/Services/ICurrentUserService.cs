namespace CDC.Web.Authorization.Services;

/// <summary>
/// Exposes the current user's id and roles. Temporarily backed by configuration
/// (<see cref="CurrentUserService"/>) until real authentication is integrated, at which point
/// this will be re-implemented to read the authenticated <c>ClaimsPrincipal</c> instead - no
/// authorization policy, handler, or consuming code needs to change when that happens.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Gets the current user's id.</summary>
    string UserId { get; }

    /// <summary>Gets the current user's assigned roles.</summary>
    IReadOnlyList<string> Roles { get; }
}
