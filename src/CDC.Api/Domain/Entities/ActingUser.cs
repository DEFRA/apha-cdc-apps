using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// The signed-in user an API request is made for: their <c>[dbo].[User]</c> row id and the role
/// flags held on it (<c>spgUser</c>).
/// </summary>
public sealed record ActingUser : BaseEntity
{
    /// <summary>Gets a value indicating whether the user can author and publish profiles.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user has policy-level profile access.</summary>
    public required bool IsPolicyProfileUser { get; init; }
}
