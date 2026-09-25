namespace CDC.Api.Features.ProfileReports.Dtos;

/// <summary>A previously generated and persisted report document.</summary>
public sealed record ProfileReportDataDto
{
    /// <summary>Gets the persisted document bytes.</summary>
    public byte[] ReportData { get; init; } = [];
}
