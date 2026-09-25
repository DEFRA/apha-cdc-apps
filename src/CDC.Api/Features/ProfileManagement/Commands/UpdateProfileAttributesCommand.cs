using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Updates a profile's title and scenario title, and applies affected species changes.
/// Mirrors the legacy <c>UpdateProfileAttributesRequest</c> data contract.
/// </summary>
public sealed record UpdateProfileAttributesCommand : IRequest<Result<UpdateProfileAttributesResultDto>>
{
    /// <summary>Gets the profile identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the new title, or empty to leave it unchanged.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the new scenario title, or empty to leave it unchanged.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets the row version read alongside the profile, so a concurrent edit can be detected.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the affected species to remove.</summary>
    public IReadOnlyList<AffectedSpeciesDeleteDto> AffectedSpeciesDeleteList { get; init; } = [];

    /// <summary>Gets the affected species to add.</summary>
    public IReadOnlyList<AffectedSpeciesInsertDto> AffectedSpeciesInsertList { get; init; } = [];
}
