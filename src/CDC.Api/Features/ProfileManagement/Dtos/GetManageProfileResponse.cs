namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// The details shown on the "Manage profile" page: titles, version pointers and status. Returned
/// by <c>GET /api/profiles/{profileId}/manage</c>.
/// </summary>
public sealed record GetManageProfileResponse
{
    /// <summary>Gets the profile identifier.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile title.</summary>
    public string ProfileTitle { get; init; } = string.Empty;

    /// <summary>Gets the "what-if" scenario title, empty for a current-situation profile.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>Gets a display label for the latest published version visible to the public.</summary>
    public string LatestPublishedVersionPublic { get; init; } = string.Empty;

    /// <summary>Gets a display label for the latest published version visible on DefraNet only.</summary>
    public string LatestPublishedVersionDefraNetOnly { get; init; } = string.Empty;

    /// <summary>Gets a display label for the latest draft version.</summary>
    public string LatestDraftVersion { get; init; } = string.Empty;

    /// <summary>Gets a display label for the profile's current status.</summary>
    public string ProfileStatus { get; init; } = string.Empty;

    /// <summary>Gets the identifier of the profile's current status, for pre-selecting the
    /// status dropdown. <see cref="Guid.Empty"/> when no status is set.</summary>
    public Guid ProfileStatusId { get; init; }

    /// <summary>
    /// Gets the profile version to browse/edit: the current draft when one exists, otherwise the
    /// current published version, otherwise the current public version. <see cref="Guid.Empty"/>
    /// when the profile has no version at all.
    /// </summary>
    public Guid CurrentProfileVersionId { get; init; }
}
