using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>
/// Resolves the <c>[dbo].[User]</c> row for an Entra ID-authenticated internal user, from the
/// claims CDC.Web read off the validated id_token. Internal users are never auto-provisioned -
/// a row must already exist, matched by <see cref="UserName"/>.
/// </summary>
public sealed record ResolveInternalUserCommand : IRequest<Result<InternalUserDto>>
{
    /// <summary>Gets the Entra ID 'oid' claim.</summary>
    public required Guid SsoUserIdInt { get; init; }

    /// <summary>
    /// Gets the Windows-style user name built from the onprem_domainname/onprem_samaccountname
    /// claims (e.g. "DOMAIN\username"), used only as a fallback the first time a user signs in
    /// through Entra ID and has no SsoUserIdInt recorded yet.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the Entra ID display name claim, used only as the <c>FullName</c> for a user with no
    /// matching <c>[dbo].[User]</c> row (legacy's "limited access" internal user - authenticated,
    /// but with no profile-authoring privileges). Ignored once a row is matched, since FullName
    /// then comes from that row instead.
    /// </summary>
    public required string FullName { get; init; }
}
