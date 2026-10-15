namespace CDC.Common.Contracts;

/// <summary>
/// What the "Profile reports" page shows for one profile version: the version's title and number,
/// whether the user may (re)generate its reports, and the reports themselves. Returned by
/// <c>GET /api/profile-reports/overview/{profileVersionId}</c>.
/// </summary>
public sealed record ProfileReportsOverviewDto
{
    /// <summary>Gets the profile version the overview covers.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the profile the version belongs to.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile's full title with its scenario title in brackets. May contain HTML.</summary>
    public string FullTitle { get; init; } = string.Empty;

    /// <summary>Gets the version number as "major.minor", for example "1.2".</summary>
    public string VersionLabel { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the version is Published rather than Draft.</summary>
    public bool IsPublished { get; init; }

    /// <summary>Gets a value indicating whether the version is the profile's current one (it has no effective-to date).</summary>
    public bool IsCurrent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the user may regenerate the reports, matching the legacy
    /// <c>CreateProfileReportCommand.CanCreateProfileReport</c>.
    /// </summary>
    public bool CanCreateReports { get; init; }

    /// <summary>Gets the reports shown in the table, in display order.</summary>
    public IReadOnlyList<ProfileReportListItemDto> Reports { get; init; } = [];
}

/// <summary>One row of the "Profile reports" table.</summary>
public sealed record ProfileReportListItemDto
{
    /// <summary>Gets the report identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the internal report name that selects the generator, for example <c>SummaryProfileGUID</c>.</summary>
    public string ReportName { get; init; } = string.Empty;

    /// <summary>Gets the report's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a PDF has been generated and stored for the report.</summary>
    public bool HasPdfData { get; init; }

    /// <summary>Gets the stored PDF's size in bytes, or zero when there is none.</summary>
    public int FileSize { get; init; }
}
