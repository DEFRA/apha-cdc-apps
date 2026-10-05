namespace CDC.Api.Features.StaticReports.Dtos;

/// <summary>
/// Request body for uploading a new static report version.
/// </summary>
public sealed record UploadStaticReportRequestDto
{
    /// <summary>Gets the report title. An existing report with this title gains a new version.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the PDF bytes to store.</summary>
    public byte[] PdfData { get; init; } = [];

    /// <summary>Gets a value indicating whether this is a user manual rather than a general report.</summary>
    public required bool IsUserManual { get; init; }

    /// <summary>Gets a value indicating whether this version is visible to unauthenticated users.</summary>
    public required bool IsPublic { get; init; }
}
