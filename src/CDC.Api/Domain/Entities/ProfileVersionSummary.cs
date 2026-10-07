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

    /// <summary>
    /// Gets a value indicating whether this version is visible to unauthenticated (public) users,
    /// rather than DefraNet-authenticated users only.
    /// </summary>
    /// <remarks>
    /// <c>spgProfileVersionInfoById</c>'s <c>IsPublic</c> column is looked up by name
    /// (not by the fixed ordinal every other column here uses) because its position has not
    /// been verified against the live schema; see <see cref="Infrastructure.Repositories.ProfileManagementRepository"/>.
    /// Defaults to <see langword="false"/> if the column cannot be found, rather than guessing.
    /// </remarks>
    public bool IsPublic { get; init; }
}
