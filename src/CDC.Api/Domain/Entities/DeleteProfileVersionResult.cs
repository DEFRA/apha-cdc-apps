namespace CDC.Api.Domain.Entities;

/// <summary>
/// Outcome of deleting a profile version, mirroring the legacy <c>DeleteProfileVersionResponse</c>
/// data contract. <c>spdProfileVersion</c> reports both which version is now latest, and whether
/// the whole profile was removed because it had no versions left.
/// </summary>
public sealed record DeleteProfileVersionResult
{
    /// <summary>
    /// Gets the identifier of the profile version that is now latest, or <see langword="null"/>
    /// when the deleted version was the last one for its profile.
    /// </summary>
    public required Guid? NextLatestProfileVersionId { get; init; }

    /// <summary>Gets a value indicating whether the parent profile was also deleted.</summary>
    public required bool IsProfileDeleted { get; init; }
}
