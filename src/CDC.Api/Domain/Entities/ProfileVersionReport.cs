using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// One report available for a profile version (whether or not it has been generated yet).
/// Mirrors the legacy <c>ProfileVersionReport</c> data contract.
/// </summary>
public sealed record ProfileVersionReport : BaseEntity
{
    /// <summary>Gets the internal report name used to select a generator (for example, <c>FullProfileGUID</c>).</summary>
    public required string ReportName { get; init; }

    /// <summary>Gets the report's display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets a value indicating whether a generated PDF has already been persisted for this report.</summary>
    public required bool HasPdfData { get; init; }

    /// <summary>Gets the persisted PDF's size in bytes, or zero when none has been generated.</summary>
    public required int FileSize { get; init; }
}
