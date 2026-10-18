using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>A global user, as returned by <c>GET /api/global-users</c>.</summary>
public sealed record GlobalUserDto : GlobalUserContract; // NOSONAR

/// <summary>Wire contract for <c>POST /api/global-users</c> and <c>PUT /api/global-users/{id}</c> on CDC.Api.</summary>
public sealed record SaveGlobalUserRequest
{
    /// <summary>Gets the user's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the username. Only used when the user does not yet exist.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the system role to assign.</summary>
    public GlobalUserRole Role { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the row version last read for this user.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>Outcome of a save or delete call, distinguishing the failure kinds the page reacts to differently.</summary>
public enum GlobalUserActionOutcome
{
    Success,
    ValidationFailed,
    Conflict,
    Error
}

/// <summary>Result of a save or delete call to <c>/api/global-users</c>.</summary>
public sealed record GlobalUserActionResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required GlobalUserActionOutcome Outcome { get; init; }

    /// <summary>Gets the failure message to show the user, when <see cref="Outcome"/> is not <see cref="GlobalUserActionOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Display helpers for <see cref="GlobalUserRole"/>.</summary>
public static class GlobalUserRoleDisplay
{
    /// <summary>Gets the display name for a role, or an empty string for <see cref="GlobalUserRole.None"/>.</summary>
    /// <param name="role">The role to describe.</param>
    /// <returns>The display name.</returns>
    public static string ToDisplayName(this GlobalUserRole role) => role switch
    {
        GlobalUserRole.ProfileEditor => "Profile editor",
        GlobalUserRole.PolicyProfileUser => "Policy profile user",
        _ => string.Empty
    };
}
