namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// Instructs a profile write operation to remove one affected species. Mirrors the legacy
/// <c>AffectedSpeciesDelete</c> data contract.
/// </summary>
public sealed record AffectedSpeciesDeleteDto
{
    /// <summary>Gets the profile version the species is being removed from.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the species being removed.</summary>
    public Guid SpeciesId { get; init; }
}
