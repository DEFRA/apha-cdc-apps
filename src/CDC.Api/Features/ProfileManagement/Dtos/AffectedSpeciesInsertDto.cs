namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// Instructs a profile or profile version write operation to add one affected species.
/// Mirrors the legacy <c>AffectedSpeciesInsert</c> data contract.
/// </summary>
public sealed record AffectedSpeciesInsertDto
{
    /// <summary>Gets the profile version the species is being added to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>
    /// Gets the profile version to copy trade/questionnaire data from, or
    /// <see cref="Guid.Empty"/> when there is nothing to clone.
    /// </summary>
    public Guid CloneProfileVersionId { get; init; }

    /// <summary>Gets the species being added.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the affected species type: <c>Profiled</c> or <c>Other</c>.</summary>
    public string Type { get; init; } = string.Empty;
}
