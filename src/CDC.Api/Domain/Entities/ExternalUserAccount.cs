namespace CDC.Api.Domain.Entities;

/// <summary>
/// An external (SSO) user as read from <c>spgaSsoUserMaintenance</c>. Distinct from
/// <see cref="ExternalUser"/>, which is the sign-in resolution result.
/// </summary>
public sealed record ExternalUserAccount
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the user's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the user's email address.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public required bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the SSO user identifier, which keys <c>spiSsoUser</c>.</summary>
    public required Guid SsoUserId { get; init; }
}
