namespace CDC.Web.Models;

/// <summary>
/// Wire contract for <c>PUT /api/profiles/{profileId}/contributors/{contributorId}</c> on CDC.Api.
/// </summary>
public sealed record UpdateContributorRequest
{
    /// <summary>Gets the contributor's new role.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets the user's full name. Ignored for an SSO user.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation. Ignored for an SSO user.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the profile sections the contributor may edit.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];

    /// <summary>Gets the row version last read for this contributor.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>
/// Outcome of a call to <c>PUT /api/profiles/{profileId}/contributors/{contributorId}</c>,
/// distinguishing the failure kinds the "Edit profile contributor" panel reacts to differently.
/// </summary>
public enum ContributorUpdateOutcome
{
    Success,
    ValidationFailed,
    Conflict,
    Error
}

/// <summary>Result of a call to <c>PUT /api/profiles/{profileId}/contributors/{contributorId}</c>.</summary>
public sealed record UpdateContributorResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required ContributorUpdateOutcome Outcome { get; init; }

    /// <summary>Gets the message to show the user, if any.</summary>
    public string? ErrorMessage { get; init; }
}
