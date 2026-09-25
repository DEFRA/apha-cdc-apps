namespace CDC.Api.Domain.Entities;

/// <summary>
/// Status of a report whose legacy generator produced PDF bytes in-process via TallPDF report
/// classes in <c>Profiles.Reports</c>, with no underlying stored procedure at all.
/// </summary>
/// <remarks>
/// TallPDF is end-of-life and is being replaced workspace-wide by a Razor views + Playwright
/// pipeline (see the <c>pdf-html-migration</c> workstream). This deliberately does not depend
/// on TallPDF: <see cref="IsAvailable"/> is <see langword="false"/> until that pipeline is
/// wired up to produce the actual document. Backs
/// <see cref="Features.ProfileReports.Dtos.ContributionsReportDto"/>,
/// <see cref="Features.ProfileReports.Dtos.ProfilePrintVersionDto"/>,
/// <see cref="Features.ProfileReports.Dtos.ProfileVersionComparisonReportDto"/>,
/// <see cref="Features.ProfileReports.Dtos.ProfileVersionBespokeReportDto"/>,
/// <see cref="Features.ProfileReports.Dtos.SummaryPrioritisationReportDto"/>,
/// <see cref="Features.ProfileReports.Dtos.SummaryProfileReportDto"/> and
/// <see cref="Features.ProfileReports.Dtos.ProfileRankingReportDto"/>, which otherwise carry no
/// domain data of their own beyond this shared status.
/// </remarks>
public sealed record PendingReport
{
    /// <summary>Gets the report's display title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a value indicating whether the report can currently be generated.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Gets a human-readable explanation of <see cref="IsAvailable"/>.</summary>
    public required string Message { get; init; }
}
