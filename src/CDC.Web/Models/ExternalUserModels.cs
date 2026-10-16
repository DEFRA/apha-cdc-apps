using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>An external user, as returned by <c>GET /api/external-users</c>.</summary>
public sealed record MaintainedExternalUserDto : ExternalUserContract; // NOSONAR

/// <summary>The outcome of saving an external user's access to a profile.</summary>
/// <param name="Succeeded">Whether the access was saved.</param>
/// <param name="ErrorMessage">Why the save failed, when it did.</param>
public sealed record ExternalUserAccessResult(bool Succeeded, string? ErrorMessage);

/// <summary>Wire contract for <c>PUT /api/external-users/{id}/review-email-subscription</c> on CDC.Api.</summary>
public sealed record SetReviewEmailSubscriptionRequest
{
    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }
}
