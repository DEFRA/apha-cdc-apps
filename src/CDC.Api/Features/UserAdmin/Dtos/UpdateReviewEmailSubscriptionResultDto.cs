namespace CDC.Api.Features.UserAdmin.Dtos;

/// <summary>
/// Outcome of changing a user's review notification email subscription.
/// </summary>
public sealed record UpdateReviewEmailSubscriptionResultDto
{
    /// <summary>Gets the user whose subscription changed.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the subscription state now stored against the user.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the user's new row version.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
