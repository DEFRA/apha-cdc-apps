namespace CDC.Api.Domain.Entities;

/// <summary>
/// Which guidance report to retrieve. Mirrors the legacy <c>ProfileGuidanceReportType</c> enum.
/// </summary>
public enum ProfileGuidanceReportType
{
    /// <summary>The full guidance report.</summary>
    All = 0,

    /// <summary>The summary profile guidance report.</summary>
    SummaryProfile = 1,

    /// <summary>The summary prioritisation guidance report.</summary>
    SummaryPrioritisationReport = 2,

    /// <summary>The QA guidance report.</summary>
    QaGuidanceReport = 3
}

/// <summary>
/// Descriptor for a profile guidance report. Mirrors the legacy <c>ProfileGuidanceReportData</c>
/// data contract, minus its raw PDF payload.
/// </summary>
/// <remarks>
/// Legacy generated <see cref="ReportType"/>'s PDF bytes in-process via TallPDF report classes
/// in <c>Profiles.Reports</c>. TallPDF is end-of-life and is being replaced workspace-wide by a
/// Razor views + Playwright pipeline (see the <c>pdf-html-migration</c> workstream) - this
/// endpoint deliberately does not depend on TallPDF. <see cref="IsAvailable"/> is
/// <see langword="false"/> until that pipeline is wired up to produce the actual PDF.
/// </remarks>
public sealed record ProfileGuidanceReport
{
    /// <summary>Gets the report requested.</summary>
    public required ProfileGuidanceReportType ReportType { get; init; }

    /// <summary>Gets the report's display title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a value indicating whether the report can currently be generated.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Gets a human-readable explanation of <see cref="IsAvailable"/>.</summary>
    public required string Message { get; init; }
}
