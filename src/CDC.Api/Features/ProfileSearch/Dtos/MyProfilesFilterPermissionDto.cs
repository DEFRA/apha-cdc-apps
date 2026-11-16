namespace CDC.Api.Features.ProfileSearch.Dtos;

/// <summary>Whether the signed-in user may filter the profile search to their own profiles.</summary>
public sealed record MyProfilesFilterPermissionDto
{
    /// <summary>
    /// Gets a value indicating whether the user is an author or a reviewer of at least one profile, which is what legacy
    /// <c>ProfilesIdentity.IsContributorOrReviewer</c> decided the "Display my own profiles only" checkbox on.
    /// </summary>
    public required bool CanFilter { get; init; }
}
