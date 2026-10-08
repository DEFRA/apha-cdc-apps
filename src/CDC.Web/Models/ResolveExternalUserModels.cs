namespace CDC.Web.Models;

/// <summary>Request body for <c>POST /api/users/external/resolve</c>.</summary>
public sealed record ResolveExternalUserRequestDto
{
    /// <summary>Gets the CIDM 'sub' claim.</summary>
    public required Guid SsoUserIdExt { get; init; }

    /// <summary>Gets the email claim.</summary>
    public required string Email { get; init; }

    /// <summary>Gets the firstName claim.</summary>
    public required string FirstName { get; init; }

    /// <summary>Gets the lastName claim.</summary>
    public required string LastName { get; init; }

    /// <summary>Gets the user's organisation, read from the relationships claim.</summary>
    public required string Organisation { get; init; }
}

/// <summary>Response body for <c>POST /api/users/external/resolve</c>, on success.</summary>
public sealed record ExternalUserDto
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the email address.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }
}

/// <summary>Outcome of <see cref="CDC.Web.Infrastructure.IApiClient.ResolveExternalUserAsync"/>.</summary>
public enum ResolveExternalUserOutcome
{
    /// <summary>The user was resolved (or provisioned) successfully.</summary>
    Success,

    /// <summary>CDC.Api denied the sign-in - the email belongs to a non-external account.</summary>
    NotPermitted,

    /// <summary>The call failed for any other reason (network, 5xx, etc).</summary>
    Error
}

/// <summary>Result of resolving an external user.</summary>
public sealed record ResolveExternalUserResult(ResolveExternalUserOutcome Outcome, ExternalUserDto? User);
