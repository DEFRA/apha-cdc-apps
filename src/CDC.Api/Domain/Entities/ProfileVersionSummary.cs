namespace CDC.Api.Domain.Entities;

/// <summary>
/// A profile version's number, read via <c>spgProfileVersionInfoById</c> for display purposes
/// (for example, on the "Manage profile" page).
/// </summary>
public sealed record ProfileVersionSummary
{
    /// <summary>Gets the major version number, incremented each time a version is published.</summary>
    public required int VersionMajor { get; init; }

    /// <summary>Gets the minor version number, incremented each time a draft is saved.</summary>
    public required int VersionMinor { get; init; }
}
