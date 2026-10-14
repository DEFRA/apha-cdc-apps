namespace CDC.Common.Contracts;

/// <summary>
/// One version of a static report or user manual. Shared by CDC.Api's response DTO and CDC.Web's
/// view model so the wire shape can never drift between the two.
/// </summary>
public abstract record StaticReportVersionContract
{
    /// <summary>Gets the version identifier, used to open, delete or download this version.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the logical report identifier, shared by every version of the report.</summary>
    public required Guid StaticReportId { get; init; }

    /// <summary>Gets the report or manual title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the major version number; displayed as <c>Major.0</c>.</summary>
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

    /// <summary>Gets a value indicating whether the current user may delete this version. Mirrors
    /// the legacy <c>StaticReport.CanDelete</c> (<c>IsCurrent AndAlso identity.IsProfileEditor</c>).
    /// Computed server-side by CDC.Api; defaults to <see langword="false"/> so existing callers
    /// that do not set it keep today's behaviour.</summary>
    public bool CanDelete { get; init; }

    /// <summary>Gets a value indicating whether this is the version currently in effect.</summary>
    public bool IsCurrent => EffectiveDateTo is null;
}
