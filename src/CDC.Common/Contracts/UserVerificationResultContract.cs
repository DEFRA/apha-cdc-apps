namespace CDC.Common.Contracts;

/// <summary>
/// Classifies a username checked against the global <c>[User]</c> table when adding a new profile
/// contributor. Mirrors the legacy <c>VerifyDatabaseUserCommand</c>/<c>spgUserAuthorisation</c>.
/// </summary>
public enum UserVerificationOutcome
{
    /// <summary>The username matches an existing global user who can be added as a contributor.</summary>
    ExistingUser = 0,

    /// <summary>The username does not exist yet, but is validly formatted for a new user.</summary>
    NewUser = 1,

    /// <summary>The username does not exist and is not validly formatted.</summary>
    InvalidFormat = 2,

    /// <summary>The username belongs to a user management system account and cannot be made a contributor.</summary>
    Blocked = 3
}

/// <summary>
/// Result of verifying a username for the "Add profile contributor" lookup step, as returned by
/// <c>GET /api/profile-contributors/verify-username</c>. Shared by CDC.Api's response DTO and
/// CDC.Web's view model so the wire shape can never drift between the two.
/// </summary>
public abstract record UserVerificationResultContract
{
    /// <summary>Gets the classification of the username.</summary>
    public UserVerificationOutcome Outcome { get; init; }

    /// <summary>Gets the identifier of the existing global user, when <see cref="Outcome"/> is
    /// <see cref="UserVerificationOutcome.ExistingUser"/>.</summary>
    public Guid? UserId { get; init; }
}
