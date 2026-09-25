namespace CDC.Api.Features.ProfileReports.Dtos;

/// <summary>One report available for a profile version.</summary>
public sealed record ProfileVersionReportDto
{
    /// <summary>Gets the report identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the internal report name used to select a generator.</summary>
    public string ReportName { get; init; } = string.Empty;

    /// <summary>Gets the report's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a generated PDF has already been persisted for this report.</summary>
    public bool HasPdfData { get; init; }

    /// <summary>Gets the persisted PDF's size in bytes, or zero when none has been generated.</summary>
    public int FileSize { get; init; }
}
