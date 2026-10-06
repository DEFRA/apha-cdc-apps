namespace CDC.Api.Domain.Entities;

/// <summary>
/// A user who is due a review notification email. Only users who are subscribed to review
/// emails and have a recorded email address are ever produced.
/// </summary>
public sealed class ReviewEmailRecipient
{
    /// <summary>Gets the user identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the sign-in name.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the address the notification is sent to.</summary>
    public string EmailAddress { get; init; } = string.Empty;
}
