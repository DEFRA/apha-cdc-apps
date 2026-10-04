namespace CDC.Common.Contracts;

/// <summary>
/// The details shown on the "Manage profile" page: titles, version pointers and status. Returned
/// by <c>GET /api/profiles/{profileId}/manage</c>. Shared by CDC.Api's response DTO and CDC.Web's
/// view model so the wire shape can never drift between the two.
/// </summary>
public abstract record ManageProfileContract
{
    /// <summary>Gets the profile identifier.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile title.</summary>
    public string ProfileTitle { get; init; } = string.Empty;

    /// <summary>Gets the "what-if" scenario title, empty for a current-situation profile.</summary>
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this profile is a "what-if" scenario, as opposed
    /// to a current-situation profile.</summary>
    public bool IsWhatIfScenario { get; init; }

    /// <summary>
    /// Gets the profile's full display title, matching the legacy
    /// <c>ProfileVersionInfo.FullTitle</c>: the scenario title in brackets after the profile
    /// title, but only for a "what-if" scenario - never for a current-situation profile.
    /// </summary>
    public string FullTitle => IsWhatIfScenario ? $"{ProfileTitle} ({ScenarioTitle})" : ProfileTitle;

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

    /// <summary>
    /// Gets a display label for the version a new draft would be created as - the legacy
    /// <c>LatestVersion</c> (current draft, else current published; never the public version)
    /// with its minor number incremented by one, for example "11.5". Empty when the profile has
    /// neither a draft nor a published version to base a new one on.
    /// </summary>
    public string NewDraftVersionLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the legacy <c>LatestVersion.Id</c>: the current draft, else the current published
    /// version - never the public version. This is the source version a new draft/published
    /// version is created from. <see cref="Guid.Empty"/> when the profile has neither.
    /// </summary>
    public Guid LatestVersionId { get; init; }

    /// <summary>Gets which action links are visible for the current user and profile state.</summary>
    public ManageProfileLinkVisibilityDto LinkVisibility { get; init; } = new();
}
