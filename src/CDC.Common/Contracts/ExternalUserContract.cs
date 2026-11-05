namespace CDC.Common.Contracts;

/// <summary>The column the external users list is sorted by.</summary>
public enum ExternalUserSortColumn
{
    /// <summary>Keep the order the database returns (the default).</summary>
    None = 0,

    /// <summary>Sort by username.</summary>
    UserName = 1,

    /// <summary>Sort by full name.</summary>
    FullName = 2,

    /// <summary>Sort by organisation.</summary>
    Organisation = 3,

    /// <summary>Sort by email address.</summary>
    EmailAddress = 4
}

/// <summary>
/// An external (SSO) user maintained by administrators, as returned by <c>GET /api/external-users</c>.
/// Shared by CDC.Api's response DTO and CDC.Web's view model so the wire shape cannot drift.
/// </summary>
public abstract record ExternalUserContract
{
    /// <summary>Gets the user's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's email address.</summary>
    public string EmailAddress { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }
}
