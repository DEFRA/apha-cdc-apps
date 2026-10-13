namespace CDC.Api.Domain.Entities;

/// <summary>One draft version of a profile, as read from the first result set of <c>spgProfileHistory</c>.</summary>
public sealed record DraftProfileVersion
{
    /// <summary>Gets the profile version's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the version's title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the scenario title, or empty for the profile's "current situation".</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>Gets the major version number.</summary>
    public int VersionMajor { get; init; }

    /// <summary>Gets the minor version number.</summary>
    public int VersionMinor { get; init; }

    /// <summary>Gets the date the version became effective.</summary>
    public DateTime EffectiveDateFrom { get; init; }

    /// <summary>Gets the date the version stopped being effective, or <see langword="null"/> for the current version.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets the profile's status name (for example "Active"), or empty when it has none.</summary>
    public string ProfileStatusName { get; init; } = string.Empty;

    /// <summary>Gets the version's title with the scenario title in brackets, matching the legacy <c>FullTitle</c>.</summary>
    public string FullTitle => ScenarioTitle.Length > 0 ? $"{Title} ({ScenarioTitle})" : Title;
}
