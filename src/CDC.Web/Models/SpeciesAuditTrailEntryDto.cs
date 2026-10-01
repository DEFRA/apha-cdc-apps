namespace CDC.Web.Models;

/// <summary>
/// A single recorded species name/parent change, as returned by
/// <c>GET /api/species/audit-trail</c> on CDC.Api.
/// </summary>
public sealed record SpeciesAuditTrailEntryDto
{
    /// <summary>Gets the audit entry identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name of the species before the change.</summary>
    public string OldName { get; init; } = string.Empty;

    /// <summary>Gets the display name of the species after the change.</summary>
    public string NewName { get; init; } = string.Empty;

    /// <summary>Gets the display name of the parent before the change.</summary>
    public string OldParent { get; init; } = string.Empty;

    /// <summary>Gets the display name of the parent after the change.</summary>
    public string NewParent { get; init; } = string.Empty;

    /// <summary>Gets the identity of the user who made the change.</summary>
    public string ChangedBy { get; init; } = string.Empty;

    /// <summary>Gets the date and time the change was recorded.</summary>
    public DateTime LogDate { get; init; }

    /// <summary>Gets the reason given for the change.</summary>
    public string ReasonForChange { get; init; } = string.Empty;
}
