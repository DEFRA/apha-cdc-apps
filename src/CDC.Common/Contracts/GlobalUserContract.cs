namespace CDC.Common.Contracts;

/// <summary>The system role a global (internal Defra) user holds.</summary>
public enum GlobalUserRole
{
    /// <summary>No global role; not a valid selection when saving.</summary>
    None = 0,

    /// <summary>Highest level of access; effectively an administrator.</summary>
    ProfileEditor = 1,

    /// <summary>Read-only access to every draft profile.</summary>
    PolicyProfileUser = 2
}

/// <summary>
/// A global user and their system role, as returned by <c>GET /api/global-users</c>. Shared by
/// CDC.Api's response DTO and CDC.Web's view model so the wire shape can never drift between the two.
/// </summary>
public abstract record GlobalUserContract
{
    /// <summary>Gets the user's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the user's system role.</summary>
    public GlobalUserRole Role { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the row version, used as the concurrency token when saving or deleting.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>Classifies a username checked in the "Add global user" lookup step.</summary>
public enum GlobalUserLookupOutcome
{
    /// <summary>The username matches an existing user who can be given a global role.</summary>
    ExistingUser = 0,

    /// <summary>The username does not exist yet, but is validly formatted for a new user.</summary>
    NewUser = 1,

    /// <summary>The username does not exist and is not validly formatted.</summary>
    InvalidFormat = 2,

    /// <summary>The username belongs to a user management system account.</summary>
    Blocked = 3,

    /// <summary>The username belongs to an SSO user, who cannot be given global access rights.</summary>
    SsoUser = 4
}

/// <summary>
/// Result of <c>GET /api/global-users/lookup</c>. For <see cref="GlobalUserLookupOutcome.ExistingUser"/>
/// and <see cref="GlobalUserLookupOutcome.NewUser"/> the user fields are populated (a new user
/// gets a freshly generated <see cref="Id"/>); otherwise they are left at their defaults.
/// </summary>
public sealed record GlobalUserLookupResult
{
    /// <summary>Gets the classification of the username.</summary>
    public GlobalUserLookupOutcome Outcome { get; init; }

    /// <summary>Gets the user's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the user's current system role.</summary>
    public GlobalUserRole Role { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the row version to send back when saving.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
