namespace CDC.Web.Models;

/// <summary>
/// The PDF content of a single static report version.
/// </summary>
public sealed record StaticReportDataDto
{
    /// <summary>Gets the stored PDF bytes.</summary>
    public byte[] PdfData { get; init; } = [];

    /// <summary>Gets the report title.</summary>
    public string Title { get; init; } = string.Empty;
}
