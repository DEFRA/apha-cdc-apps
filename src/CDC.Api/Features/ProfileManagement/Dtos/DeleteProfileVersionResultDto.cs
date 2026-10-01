namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// Outcome of deleting a profile version. Returned by
/// <c>DELETE /api/profiles/versions/{profileVersionId}</c>.
/// </summary>
public sealed record DeleteProfileVersionResultDto
{
    /// <summary>
    /// Gets the identifier of the profile version that is now latest, or <see langword="null"/>
    /// when the deleted version was the last one for its profile.
    /// </summary>
    public Guid? NextLatestProfileVersionId { get; init; }

    /// <summary>Gets a value indicating whether the parent profile was also deleted.</summary>
    public bool IsProfileDeleted { get; init; }
}
