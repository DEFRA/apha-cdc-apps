using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// One version of a static report (general report or user manual), as returned by
/// <c>spgaCurrentStaticReport</c>/<c>spgStaticReportHistory</c>. Mirrors the legacy
/// <c>StaticReportVersion</c> data contract and <c>StaticReport</c> business object.
/// </summary>
public sealed record StaticReportVersion : BaseEntity
{
    /// <summary>Gets the identifier shared by every version of this report.</summary>
    public required Guid StaticReportId { get; init; }

    /// <summary>Gets the report title, e.g. "D2R2 Quality Statement".</summary>
    public required string Title { get; init; }

    /// <summary>Gets the version number, incremented on every upload.</summary>
    public required byte VersionMajor { get; init; }

    /// <summary>Gets when this version became effective.</summary>
    public required DateTime EffectiveDateFrom { get; init; }

    /// <summary>Gets when this version was superseded, or <see langword="null"/> when current.</summary>
    public DateTime? EffectiveDateTo { get; init; }

    /// <summary>Gets a value indicating whether this is the current (not superseded) version.</summary>
    public bool IsCurrent => EffectiveDateTo is null;

    /// <summary>Gets a value indicating whether this is a user manual rather than a general report.</summary>
    public required bool IsUserManual { get; init; }

    /// <summary>Gets a value indicating whether this version is visible to unauthenticated users.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>Gets the size, in bytes, of the stored PDF.</summary>
    public required int FileSize { get; init; }
}
