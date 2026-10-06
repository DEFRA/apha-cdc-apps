namespace CDC.Api.Features.UserAdmin.Dtos;

/// <summary>
/// A user account that an administrator can maintain.
/// </summary>
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

    /// <summary>Gets the row version read with the user. Send it back when updating the subscription.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
