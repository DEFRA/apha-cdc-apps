namespace CDC.Api.Domain.Entities;

/// <summary>
/// A previously generated and persisted report document. Mirrors the legacy
/// <c>ProfileReportData</c> data contract.
/// </summary>
public sealed record ProfileReportData
{
    /// <summary>Gets the persisted document bytes.</summary>
    public required byte[] ReportData { get; init; }
}
