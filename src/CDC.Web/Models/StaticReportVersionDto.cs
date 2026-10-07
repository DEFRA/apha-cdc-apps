namespace CDC.Web.Models;

/// <summary>
/// One version of a static report (general report or user manual) returned by
/// <c>GET /api/static-reports</c> on CDC.Api. Field names and types mirror the API's
/// <c>StaticReportVersionDto</c> exactly, so this deserialises directly from JSON.
/// One version of a static report (general report or user manual).
/// </summary>
public sealed record StaticReportVersionDto
{
    /// <summary>Gets the version identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier shared by every version of this report.</summary>
    public Guid StaticReportId { get; init; }

    /// <summary>Gets the report title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the version number, incremented on every upload.</summary>
    public byte VersionMajor { get; init; }

    /// <summary>Gets when this version became effective.</summary>
    public DateTime EffectiveDateFrom { get; init; }

    /// <summary>Gets when this version was superseded, or <see langword="null"/> when current.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets a value indicating whether this is the current (not superseded) version.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Gets a value indicating whether this is a user manual rather than a general report.</summary>
    public bool IsUserManual { get; init; }

    /// <summary>Gets a value indicating whether this version is visible to unauthenticated users.</summary>
    public bool IsPublic { get; init; }

    /// <summary>Gets the size, in bytes, of the stored PDF.</summary>
    public int FileSize { get; init; }
}
