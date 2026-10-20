namespace CDC.Web.Models;

/// <summary>Request body for <c>POST /api/users/internal/resolve</c>.</summary>
public sealed record ResolveInternalUserRequestDto
{
    /// <summary>Gets the Entra ID 'oid' claim.</summary>
    public required Guid SsoUserIdInt { get; init; }

    /// <summary>Gets the Windows-style user name built from the onprem_domainname/onprem_samaccountname claims.</summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the Entra ID display name claim, used only as the <c>FullName</c> of a newly created
    /// <c>[dbo].[User]</c> row.
    /// </summary>
    public required string FullName { get; init; }
}

/// <summary>
/// Response body for <c>POST /api/users/internal/resolve</c>, on success. A user with no
/// <c>[dbo].[User]</c> row yet is created first, with both role flags <see langword="false"/>.
/// </summary>
public sealed record InternalUserDto
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets a value indicating whether the user can author/publish profiles.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user is a policy profile user (contributions report only).</summary>
    public required bool IsPolicyProfileUser { get; init; }
}

/// <summary>Outcome of <see cref="CDC.Web.Infrastructure.IApiClient.ResolveInternalUserAsync"/>.</summary>
public enum ResolveInternalUserOutcome
{
    /// <summary>The user was resolved successfully.</summary>
    Success,

    /// <summary>CDC.Api denied the sign-in (reserved; currently never returned for internal users).</summary>
    NotPermitted,

    /// <summary>The call failed for any other reason (network, 5xx, etc).</summary>
    Error
}

/// <summary>Result of resolving an internal user.</summary>
public sealed record ResolveInternalUserResult(ResolveInternalUserOutcome Outcome, InternalUserDto? User);
