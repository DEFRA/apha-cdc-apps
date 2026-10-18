using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>Outcome of asking CDC.Api for a profile's contributions report data.</summary>
public enum ContributionsReportOutcome
{
    /// <summary>The report data was returned.</summary>
    Success,

    /// <summary>The profile has no draft versions to report on.</summary>
    NotFound,

    /// <summary>The user may not run the contributions report.</summary>
    Forbidden,

    /// <summary>The request failed for any other reason.</summary>
    Error
}

/// <summary>Result of <c>GET /api/profile-reports/contributions/{profileId}</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Report">The report data when <paramref name="Outcome"/> is <see cref="ContributionsReportOutcome.Success"/>.</param>
public sealed record ContributionsReportResult(ContributionsReportOutcome Outcome, ContributionsReportDto? Report = null);

/// <summary>The contributions report as laid out for the PDF.</summary>
/// <param name="ProfileTitle">The profile's title as plain text.</param>
/// <param name="Versions">The draft versions, newest first.</param>
public sealed record ContributionsReportViewModel(string ProfileTitle, IReadOnlyList<ContributionsReportVersionView> Versions);

/// <summary>One draft version's page(s) in the report.</summary>
/// <param name="Heading">The version heading, for example "Title - v1.0 (Draft) Valid from 11/09/2026".</param>
/// <param name="Sections">Every profile section with its contributions.</param>
public sealed record ContributionsReportVersionView(string Heading, IReadOnlyList<ContributionsReportSectionView> Sections);

/// <summary>One profile section's contributions table.</summary>
/// <param name="Heading">The section heading, for example "1. Summary".</param>
/// <param name="Rows">The contributions; empty shows "No contributions".</param>
public sealed record ContributionsReportSectionView(string Heading, IReadOnlyList<ContributionsReportRowView> Rows);

/// <summary>One contribution row.</summary>
/// <param name="FullName">The contributor's full name.</param>
/// <param name="Organisation">The contributor's organisation.</param>
/// <param name="ContributionDate">The contribution date as displayed.</param>
public sealed record ContributionsReportRowView(string FullName, string Organisation, string ContributionDate);
