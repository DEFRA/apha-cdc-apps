namespace CDC.Common.Contracts;

/// <summary>
/// The data behind a profile's contributions report, as returned by
/// <c>GET /api/profile-reports/contributions/{profileId}</c>: every draft version of the profile,
/// newest first, and each of its sections' logged contributions. Replaces the legacy
/// <c>ContributionReportPdf</c> document model.
/// </summary>
public sealed record ContributionsReportDto
{
    /// <summary>Gets the profile the report covers.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile's full title (with its scenario title in brackets), taken from the newest draft version. May contain HTML.</summary>
    public string ProfileTitle { get; init; } = string.Empty;

    /// <summary>Gets the profile's draft versions, newest first.</summary>
    public IReadOnlyList<ContributionsReportVersionDto> Versions { get; init; } = [];
}

/// <summary>One draft version in the contributions report.</summary>
public sealed record ContributionsReportVersionDto
{
    /// <summary>Gets the profile version's identifier.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the version's full title (with its scenario title in brackets). May contain HTML.</summary>
    public string FullTitle { get; init; } = string.Empty;

    /// <summary>Gets the major version number.</summary>
    public int VersionMajor { get; init; }

    /// <summary>Gets the minor version number.</summary>
    public int VersionMinor { get; init; }

    /// <summary>Gets a value indicating whether this is the profile's current version (it has no effective-to date).</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Gets the date the version became effective.</summary>
    public DateTime EffectiveDateFrom { get; init; }

    /// <summary>Gets the date the version stopped being effective, or <see langword="null"/> for the current version.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets the profile's status name (for example "Active"), shown for the current draft version.</summary>
    public string ProfileStatusName { get; init; } = string.Empty;

    /// <summary>Gets the profile sections with their contributions, in section-number order.</summary>
    public IReadOnlyList<ContributionsReportSectionDto> Sections { get; init; } = [];
}

/// <summary>One profile section's contributions within a version.</summary>
public sealed record ContributionsReportSectionDto
{
    /// <summary>Gets the section's number.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the section's name.</summary>
    public string SectionName { get; init; } = string.Empty;

    /// <summary>Gets the logged contributions, most recent first; empty when nobody has contributed.</summary>
    public IReadOnlyList<ProfileSectionContributionDto> Contributions { get; init; } = [];
}
