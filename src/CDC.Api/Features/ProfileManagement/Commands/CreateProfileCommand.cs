using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Creates a new profile, its initial draft profile version, and any affected species. Mirrors
/// the legacy <c>CreateProfileRequest</c> data contract.
/// </summary>
/// <remarks>
/// When <see cref="ParentId"/> is supplied, this creates a "what-if" scenario belonging to an
/// existing current-situation profile, and the initial version's title is taken from
/// <see cref="ParentTitle"/> rather than <see cref="Title"/>, exactly as the legacy
/// <c>ProfileManagementService.CreateProfile</c> did.
/// </remarks>
public sealed record CreateProfileCommand : IRequest<Result<CreateProfileResultDto>>
{
    /// <summary>Gets the identifier to assign to the new profile.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier to assign to the new profile's initial draft version.</summary>
    public Guid CurrentDraftProfileVersionId { get; init; }

    /// <summary>
    /// Gets the profile version to clone data from, or <see cref="Guid.Empty"/> for a brand
    /// new profile with no history.
    /// </summary>
    public Guid CloneProfileVersionId { get; init; }

    /// <summary>Gets the profile title. Ignored when <see cref="ParentId"/> is supplied.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the "what-if" scenario title.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the current-situation profile this scenario belongs to, or
    /// <see cref="Guid.Empty"/> to create a current-situation profile rather than a scenario.
    /// </summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the parent (current-situation) profile's title.</summary>
    public string ParentTitle { get; init; } = string.Empty;

    /// <summary>Gets the initial profile status, or <see cref="Guid.Empty"/> to use the database default.</summary>
    public Guid ProfileStatusId { get; init; }

    /// <summary>Gets the species to affect the new profile version.</summary>
    public IReadOnlyList<AffectedSpeciesInsertDto> AffectedSpeciesInsertList { get; init; } = [];
}
