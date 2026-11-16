namespace CDC.Web.Models;

/// <summary>Whether the signed-in user may filter the profile search to their own profiles, from
/// <c>GET /api/profile-search/my-profiles-permission</c>.</summary>
public sealed record MyProfilesFilterPermissionDto
{
    /// <summary>Gets a value indicating whether the user is an author or a reviewer of at least one profile.</summary>
    public required bool CanFilter { get; init; }
}
