namespace CDC.Web.Models;

/// <summary>
/// The details shown on the "Manage profile" page, as returned by
/// <c>GET /api/profiles/{profileId}/manage</c>.
/// </summary>
public sealed record ManageProfileViewModel
{
    public Guid ProfileId { get; init; }
    public string ProfileTitle { get; init; } = string.Empty;
    public string ScenarioTitle { get; init; } = string.Empty;
    public string LatestPublishedVersionPublic { get; init; } = string.Empty;
    public string LatestPublishedVersionDefraNetOnly { get; init; } = string.Empty;
    public string LatestDraftVersion { get; init; } = string.Empty;
    public string ProfileStatus { get; init; } = string.Empty;
    public Guid ProfileStatusId { get; init; }
}
