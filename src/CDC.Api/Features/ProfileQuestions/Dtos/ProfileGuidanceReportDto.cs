using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.ProfileQuestions.Dtos;

/// <summary>
/// Descriptor for a profile guidance report. <see cref="IsAvailable"/> is
/// <see langword="false"/> until the Razor + Playwright PDF pipeline that replaces TallPDF is
/// wired up to produce the actual document; no PDF bytes are returned by this endpoint.
/// </summary>
public sealed record ProfileGuidanceReportDto
{
    /// <summary>Gets the report requested.</summary>
    public ProfileGuidanceReportType ReportType { get; init; }

    /// <summary>Gets the report's display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the report can currently be generated.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Gets a human-readable explanation of <see cref="IsAvailable"/>.</summary>
    public string Message { get; init; } = string.Empty;
}
