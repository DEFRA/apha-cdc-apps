namespace CDC.Api.Features.ProfileReports.Dtos;

/// <summary>
/// Which disease ranking report to retrieve. Mirrors the legacy
/// <c>GetProfileRankingReportRequest.RankingReportType</c> enum.
/// </summary>
public enum ProfileRankingReportType
{
    /// <summary>Every species.</summary>
    All = 0,

    /// <summary>Fish species only.</summary>
    Fish = 1,

    /// <summary>Terrestrial species only.</summary>
    Terrestrial = 2
}

/// <summary>
/// A disease ranking report. <see cref="IsAvailable"/> is <see langword="false"/> until the
/// Razor + Playwright PDF pipeline that replaces TallPDF is wired up to produce the actual
/// document; no PDF bytes are returned by this endpoint.
/// </summary>
public sealed record ProfileRankingReportDto
{
    /// <summary>Gets the report requested.</summary>
    public ProfileRankingReportType ReportType { get; init; }

    /// <summary>
    /// Gets the named filter to apply, only meaningful when <see cref="ReportType"/> is
    /// <see cref="ProfileRankingReportType.All"/>.
    /// </summary>
    public string? NameOfFilter { get; init; }

    /// <summary>Gets the report's display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the report can currently be generated.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Gets a human-readable explanation of <see cref="IsAvailable"/>.</summary>
    public string Message { get; init; } = string.Empty;
}
