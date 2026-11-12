using CDC.Common.Contracts;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A user holding a global system role, as read from <c>spgaGlobalUser</c>/<c>spgUser</c>.
/// </summary>
public sealed record GlobalUser
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the user's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public required bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the user's system role.</summary>
    public required GlobalUserRole Role { get; init; }

    /// <summary>Gets a value indicating whether this is an SSO user, who cannot hold a global role.</summary>
    public bool IsSsoUser { get; init; }

    /// <summary>Gets the row version, used as the concurrency token when saving or deleting.</summary>
    public required byte[] LastUpdated { get; init; }

    /// <summary>
    /// Gets the email address read from the directory, recorded only when a new user is created so the user can be matched and
    /// emailed before their first sign-in. Never changes an existing user.
    /// </summary>
    public string? EmailAddress { get; init; }

    /// <summary>
    /// Gets the Entra ID object id read from the directory, recorded only when a new user is created so the user is matched by it
    /// from their first sign-in. Never changes an existing user.
    /// </summary>
    public Guid? SsoUserIdInt { get; init; }
}
