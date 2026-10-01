namespace CDC.Api.Features.StaticReports.Dtos;

/// <summary>
/// One version of a static report or user manual, as shown on the legacy
/// <c>StaticReports.aspx</c> grid.
/// </summary>
public sealed record StaticReportDto
{
    /// <summary>Gets the version identifier, used to open, delete or download this version.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the logical report identifier, shared by every version of the report.</summary>
    public required Guid StaticReportId { get; init; }

    /// <summary>Gets the report or manual title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the major version number; the legacy page displays it as <c>Major.0</c>.</summary>
    public required int VersionMajor { get; init; }

    /// <summary>Gets the date this version became effective.</summary>
    public required DateTime EffectiveDateFrom { get; init; }

    /// <summary>Gets the date this version was superseded, or <see langword="null"/> while it is current.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets a value indicating whether this is a user manual rather than a general report.</summary>
    public required bool IsUserManual { get; init; }

    /// <summary>Gets a value indicating whether this version is visible to unauthenticated users.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>Gets the stored document size in bytes.</summary>
    public required int FileSize { get; init; }

    /// <summary>Gets a value indicating whether this is the version currently in effect.</summary>
    public bool IsCurrent => EffectiveDateTo is null;
}

/// <summary>The stored document bytes for one static report version.</summary>
public sealed record StaticReportDataDto
{
    /// <summary>Gets the title, used as the download file name.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the PDF bytes held in the database.</summary>
    public required byte[] PdfData { get; init; }
}
