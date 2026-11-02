namespace CDC.Web.Models;

/// <summary>One selectable profile version in the Compare profile versions tree.</summary>
/// <param name="VersionId">The profile version identifier.</param>
/// <param name="FullLabel">Profile, lineage and version text, for example "Anthrax - Current situation - Version 2.0, Published, effective 01/01/2024 to 01/02/2025".</param>
public sealed record ProfileVersionOption(Guid VersionId, string FullLabel);

/// <summary>The Compare profile versions tree plus a lookup of every selectable version in it.</summary>
public sealed record ProfileVersionTree(
    TreeViewViewModel Tree,
    IReadOnlyDictionary<Guid, ProfileVersionOption> Versions);
