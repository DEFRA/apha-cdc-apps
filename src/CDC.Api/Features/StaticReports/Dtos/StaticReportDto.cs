using CDC.Common.Contracts;

namespace CDC.Api.Features.StaticReports.Dtos;

/// <summary>
/// One version of a static report or user manual, as shown on the legacy
/// <c>StaticReports.aspx</c> grid.
/// </summary>
public sealed record StaticReportDto : StaticReportVersionContract; // NOSONAR

/// <summary>The stored document bytes for one static report version.</summary>
public sealed record StaticReportDataDto
{
    /// <summary>Gets the title, used as the download file name.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the PDF bytes held in the database.</summary>
    public required byte[] PdfData { get; init; }
}

/// <summary>Whether the current user may upload static reports or user manuals.</summary>
public sealed record StaticReportUploadPermissionDto
{
    /// <summary>Gets a value indicating whether the current user may upload documents.</summary>
    public required bool CanUpload { get; init; }
}
