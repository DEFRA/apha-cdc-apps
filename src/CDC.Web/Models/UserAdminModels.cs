namespace CDC.Web.Models;

/// <summary>A user account that an administrator can maintain.</summary>
public sealed record MaintainedUserDto
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

    /// <summary>Gets a value indicating whether this is an external (single sign-on) user.</summary>
    public bool IsExternal { get; init; }

    /// <summary>Gets the row version read with the user, for optimistic concurrency.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>Request body for changing a user's review notification email subscription.</summary>
public sealed record UpdateReviewEmailSubscriptionRequest
{
    /// <summary>Gets the user whose subscription is changing.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets a value indicating whether the user should receive review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the row version read with the user.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>Outcome of a review email subscription update.</summary>
public enum ReviewEmailSubscriptionOutcome
{
    /// <summary>The subscription was stored.</summary>
    Success,

    /// <summary>The request was rejected as invalid.</summary>
    ValidationFailed,

    /// <summary>No such user exists.</summary>
    NotFound,

    /// <summary>Another administrator saved the user first.</summary>
    Conflict,

    /// <summary>The update failed for an unexpected reason.</summary>
    Error
}

/// <summary>Result of attempting to change a user's review email subscription.</summary>
public sealed record UpdateReviewEmailSubscriptionResult(ReviewEmailSubscriptionOutcome Outcome, string? ErrorMessage);

/// <summary>Model for the shared user review email subscription table partial.</summary>
/// <param name="UserType">Which list is being shown: <c>global</c> or <c>external</c>.</param>
/// <param name="Caption">Visually hidden table caption.</param>
/// <param name="ShowEmailAddress">Whether to show the email address column.</param>
/// <param name="Users">The users to list.</param>
public sealed record UserSubscriptionTableViewModel(
    string UserType,
    string Caption,
    bool ShowEmailAddress,
    IReadOnlyList<MaintainedUserDto> Users);
