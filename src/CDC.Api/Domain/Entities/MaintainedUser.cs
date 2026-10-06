namespace CDC.Api.Domain.Entities;

/// <summary>
/// A user account as shown on the Maintain global users and Maintain external users screens.
/// </summary>
public sealed class MaintainedUser
{
    /// <summary>Gets the user identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the sign-in name.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the organisation the user belongs to.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the address review notifications are sent to, when one is recorded.</summary>
    public string? EmailAddress { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets a value indicating whether the user holds the profile editor role.</summary>
    public bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user holds the policy profile user role.</summary>
    public bool IsPolicyProfileUser { get; init; }

    /// <summary>Gets the single sign-on identifier; empty for an internal (global) user.</summary>
    public Guid SsoUserId { get; init; }

    /// <summary>Gets the row version read with the user, for optimistic concurrency.</summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets a value indicating whether this is an external (single sign-on) user.</summary>
    public bool IsExternal => SsoUserId != Guid.Empty;
}
