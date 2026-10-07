namespace CDC.Api.Domain.Entities;

/// <summary>
/// The PDF content of a single static report version, as returned by
/// <c>spgStaticReportVersionData</c>.
/// </summary>
public sealed record StaticReportData
{
    /// <summary>Gets the stored PDF bytes.</summary>
    public required byte[] PdfData { get; init; }

    /// <summary>Gets a value indicating whether this version is visible to unauthenticated users.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>Gets the report title.</summary>
    public required string Title { get; init; }
}
