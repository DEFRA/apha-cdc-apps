using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>
/// Resolves, or creates, the <c>[dbo].[User]</c> row for an Entra ID-authenticated internal user, from
/// the claims CDC.Web read off the validated id_token. A user with no row is created with no
/// privileges.
/// </summary>
public sealed record ResolveInternalUserCommand : IRequest<Result<InternalUserDto>>
{
    /// <summary>Gets the Entra ID 'oid' claim.</summary>
    public required Guid SsoUserIdInt { get; init; }

    /// <summary>
    /// Gets the Windows-style user name built from the onprem_domainname/onprem_samaccountname
    /// claims (e.g. "DOMAIN\username"), used as a fallback the first time a user signs in through
    /// Entra ID and has no SsoUserIdInt recorded yet, and as the user name of a newly created row.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the Entra ID display name claim, used only as the <c>FullName</c> of a newly created
    /// <c>[dbo].[User]</c> row (the user name when empty). Ignored once a row is matched, since
    /// FullName then comes from that row instead.
    /// </summary>
    public required string FullName { get; init; }
}
