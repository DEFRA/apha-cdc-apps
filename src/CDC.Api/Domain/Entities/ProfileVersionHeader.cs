namespace CDC.Api.Domain.Entities;

/// <summary>The title, number, state and dates of one profile version, read from <c>spgProfileVersionInfoById</c>.</summary>
public sealed record ProfileVersionHeader
{
    /// <summary>Gets the profile version's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the profile the version belongs to.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile's title. May contain HTML.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the scenario title, or empty for the profile's "current situation".</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>Gets the major version number.</summary>
    public int VersionMajor { get; init; }

    /// <summary>Gets the minor version number.</summary>
    public int VersionMinor { get; init; }

    /// <summary>Gets a value indicating whether the version is Published rather than Draft.</summary>
    public bool IsPublished { get; init; }

    /// <summary>Gets the date the version became effective.</summary>
    public DateTime EffectiveDateFrom { get; init; }

    /// <summary>Gets the date the version stopped being effective, or <see langword="null"/> for the current version.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets a value indicating whether the version is visible to the public.</summary>
    public bool IsPublic { get; init; }

    /// <summary>Gets the profile's status name (for example "Validation complete"), or empty when it has none.</summary>
    public string ProfileStatusName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this is the profile's current version, matching the legacy <c>IsCurrent</c>.</summary>
    public bool IsCurrent => EffectiveDateTo is null;

    /// <summary>Gets the version number as "major.minor".</summary>
    public string VersionLabel => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{VersionMajor}.{VersionMinor}");

    /// <summary>Gets the title with the scenario title in brackets, matching the legacy <c>FullTitle</c>.</summary>
    public string FullTitle => ScenarioTitle.Length > 0 ? $"{Title} ({ScenarioTitle})" : Title;

    /// <summary>
    /// Describes the version exactly as the legacy <c>ProfileVersionInfo.ToString()</c> did, for example
    /// "Anthrax - v1.0 (Published) Valid from 01/01/2020". The profile status name is appended in
    /// brackets only for a current draft that has one.
    /// </summary>
    /// <returns>The description. The title may contain HTML.</returns>
    public string ToDescription()
    {
        var state = IsPublished ? "Published" : "Draft";
        var effectiveDates = IsCurrent
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Valid from {EffectiveDateFrom:dd/MM/yyyy}")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Valid from {EffectiveDateFrom:dd/MM/yyyy} to {EffectiveDateTo:dd/MM/yyyy}");
        var description = $"{FullTitle} - v{VersionLabel} ({state}) {effectiveDates}";

        return IsCurrent && !IsPublished && ProfileStatusName.Length > 0
            ? $"{description} ({ProfileStatusName})"
            : description;
    }
}
