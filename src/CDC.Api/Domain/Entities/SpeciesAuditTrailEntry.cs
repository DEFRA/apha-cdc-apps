namespace CDC.Api.Domain.Entities;

/// <summary>
/// A single recorded name/parent change for a species. Sourced from
/// <c>spgaSpeciesTableAuditLog</c>, which reads the audit rows written by <c>spuSpecies</c>.
/// </summary>
public sealed record SpeciesAuditTrailEntry
{
    /// <summary>Gets the audit entry identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name of the species before the change.</summary>
    public required string OldName { get; init; }

    /// <summary>Gets the display name of the species after the change.</summary>
    public required string NewName { get; init; }

    /// <summary>Gets the display name of the parent before the change.</summary>
    public required string OldParent { get; init; }

    /// <summary>Gets the display name of the parent after the change.</summary>
    public required string NewParent { get; init; }

    /// <summary>Gets the identity of the user who made the change.</summary>
    public required string ChangedBy { get; init; }

    /// <summary>Gets the date and time the change was recorded.</summary>
    public required DateTime LogDate { get; init; }

    /// <summary>Gets the reason given for the change.</summary>
    public required string ReasonForChange { get; init; }
}
