using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>
/// Resolves (or provisions) the <c>[dbo].[User]</c> row for a CIDM-authenticated external user,
/// from the claims CDC.Web read off the validated id_token.
/// </summary>
public sealed record ResolveExternalUserCommand : IRequest<Result<ExternalUserDto>>
{
    /// <summary>Gets the CIDM 'sub' claim.</summary>
    public required Guid CidmSsoId { get; init; }

    /// <summary>Gets the email claim.</summary>
    public required string Email { get; init; }

    /// <summary>Gets the firstName claim. Only used when provisioning a new user.</summary>
    public required string FirstName { get; init; }

    /// <summary>Gets the lastName claim. Only used when provisioning a new user.</summary>
    public required string LastName { get; init; }

    /// <summary>Gets the user's organisation. Only used when provisioning a new user.</summary>
    public required string Organisation { get; init; }
}
