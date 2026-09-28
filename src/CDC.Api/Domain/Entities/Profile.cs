using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A profile: the aggregate root for a disease/species surveillance profile and its versions.
/// Mirrors the legacy <c>ProfileAttributes</c> data contract returned by
/// <c>ProfileManagementService.GetProfileAttributes</c> (<c>spgProfile</c>).
/// </summary>
public sealed record Profile : BaseEntity
{
    /// <summary>Gets the profile title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the "what-if" scenario title, empty for a current-situation profile.</summary>
    public required string ScenarioTitle { get; init; }

    /// <summary>
    /// Gets the identifier of the current-situation profile this scenario belongs to, or
    /// <see cref="Guid.Empty"/> when this profile is not a scenario.
    /// </summary>
    public required Guid ParentId { get; init; }

    /// <summary>Gets the parent (current-situation) profile's title.</summary>
    public required string ParentTitle { get; init; }

    /// <summary>Gets the identifier of the current draft profile version.</summary>
    public required Guid CurrentDraftProfileVersionId { get; init; }

    /// <summary>Gets the identifier of the current published profile version.</summary>
    public required Guid CurrentPublishedProfileVersionId { get; init; }

    /// <summary>Gets the identifier of the current publicly visible profile version.</summary>
    public required Guid CurrentPublicVersionId { get; init; }

    /// <summary>Gets a value indicating whether any scenario of this profile is public.</summary>
    public required bool HasPublicScenarios { get; init; }

    /// <summary>Gets the profile's current status.</summary>
    public required Guid ProfileStatusId { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> used for optimistic concurrency.</summary>
    public required byte[] LastUpdated { get; init; }

    /// <summary>Gets the species affected by this profile's current version.</summary>
    public required IReadOnlyList<AffectedSpeciesInfo> AffectedSpecies { get; init; }
}
