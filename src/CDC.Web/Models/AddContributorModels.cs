namespace CDC.Web.Models;

/// <summary>
/// Wire contract for <c>POST /api/profiles/{profileId}/contributors</c> on CDC.Api.
/// </summary>
public sealed record AddContributorRequest
{
    /// <summary>Gets the user id: the looked-up existing global user's id, or a freshly generated
    /// id for a brand-new username.</summary>
    public Guid ContributorId { get; init; }

    /// <summary>Gets the contributor's username. Only used when <see cref="ContributorId"/> does
    /// not yet exist as a global user.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the looked-up global user is an SSO user. Always
    /// <see langword="false"/> for a brand-new username.</summary>
    public bool IsSsoUser { get; init; }

    /// <summary>Gets the contributor's new role.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets the user's full name. Ignored for an SSO user.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation. Ignored for an SSO user.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the profile sections the contributor may edit.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];
}

/// <summary>
/// Outcome of a call to <c>POST /api/profiles/{profileId}/contributors</c>, distinguishing the
/// failure kinds the "Add profile contributor" panel reacts to differently.
/// </summary>
public enum ContributorAddOutcome
{
    Success,
    ValidationFailed,
    Conflict,
    Error
}

/// <summary>Result of a call to <c>POST /api/profiles/{profileId}/contributors</c>.</summary>
public sealed record AddContributorResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required ContributorAddOutcome Outcome { get; init; }

    /// <summary>Gets the failure message to show the user, when <see cref="Outcome"/> is not <see cref="ContributorAddOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Outcome of a call to <c>DELETE /api/profiles/{profileId}/contributors/{contributorId}</c>,
/// distinguishing the failure kinds the contributors list reacts to differently.
/// </summary>
public enum ContributorDeleteOutcome
{
    Success,
    Conflict,
    Error
}

/// <summary>Result of a call to <c>DELETE /api/profiles/{profileId}/contributors/{contributorId}</c>.</summary>
public sealed record DeleteContributorResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required ContributorDeleteOutcome Outcome { get; init; }

    /// <summary>Gets the failure message to show the user, when <see cref="Outcome"/> is not <see cref="ContributorDeleteOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
