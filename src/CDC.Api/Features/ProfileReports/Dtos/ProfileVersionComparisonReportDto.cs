namespace CDC.Api.Features.ProfileReports.Dtos;

/// <summary>
/// A comparison between two profile versions. <see cref="IsAvailable"/> is
/// <see langword="false"/> until the Razor + Playwright PDF pipeline that replaces TallPDF is
/// wired up to produce the actual document; no PDF bytes are returned by this endpoint.
/// </summary>
public sealed record ProfileVersionComparisonReportDto
{
    /// <summary>Gets the earlier profile version being compared.</summary>
    public Guid SourceVersionId { get; init; }

    /// <summary>Gets the later profile version being compared.</summary>
    public Guid TargetVersionId { get; init; }

    /// <summary>Gets the report's display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the report can currently be generated.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Gets a human-readable explanation of <see cref="IsAvailable"/>.</summary>
    public string Message { get; init; } = string.Empty;
}
