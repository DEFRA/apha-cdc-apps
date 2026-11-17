namespace CDC.Web.Models;

/// <summary>The kind of sign-in a user is looked up for.</summary>
public enum UserLookupKind
{
    /// <summary>An internal sign-in, which the identity provider would do through Entra ID.</summary>
    Internal,

    /// <summary>An external sign-in, which the identity provider would do through CIDM.</summary>
    External
}

/// <summary>Response body for <c>POST /api/users/internal/lookup</c> and <c>POST /api/users/external/lookup</c>, on success.</summary>
public sealed record UserLookupDto
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets a value indicating whether the user can author and publish profiles.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user is a policy profile user (contributions report only).</summary>
    public required bool IsPolicyProfileUser { get; init; }

    /// <summary>Gets a value indicating whether the user is an internal profile editor, who signs in through CIDM.</summary>
    public required bool IsInternalProfileEditor { get; init; }
}

/// <summary>Outcome of <see cref="CDC.Web.Infrastructure.IApiClient.LookUpUserByEmailAsync"/>.</summary>
public enum UserLookupOutcome
{
    /// <summary>One user holds the email address and could sign in as that kind of user.</summary>
    Found,

    /// <summary>No user holds the email address.</summary>
    NotFound,

    /// <summary>The user holding the email address could not sign in as that kind of user.</summary>
    NotPermitted,

    /// <summary>Several users hold the email address, so none can be chosen.</summary>
    Ambiguous,

    /// <summary>CDC.Api did not accept the text as an email address.</summary>
    InvalidEmailAddress,

    /// <summary>The call failed for any other reason (network, 5xx, etc).</summary>
    Error
}

/// <summary>Result of looking a user up by email address.</summary>
public sealed record UserLookupResult(UserLookupOutcome Outcome, UserLookupDto? User);
