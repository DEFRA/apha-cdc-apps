using System.Text.Json.Serialization;
using CDC.Api.Domain.Common;
using CDC.Api.Features.Users.Dtos;
using MediatR;

namespace CDC.Api.Features.Users.Commands;

/// <summary>
/// Looks up the <c>[dbo].[User]</c> row that holds an email address, so CDC.Web can sign that user in without the identity
/// provider while LoadTesting is on. Read-only: no row is created or changed.
/// </summary>
public sealed record LookUpUserByEmailCommand : IRequest<Result<UserLookupDto>>
{
    /// <summary>Gets the email address to look up.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Gets the kind of sign-in the user is wanted for. Set from the route, so it is never read from the request body.</summary>
    [JsonIgnore]
    public UserLookupKind Kind { get; init; }
}
