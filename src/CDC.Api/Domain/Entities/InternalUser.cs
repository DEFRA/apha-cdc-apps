using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A <c>[dbo].[User]</c> row resolved for an authenticated Microsoft Entra ID (internal) user.
/// External users are resolved through a different mechanism (<see cref="ExternalUser"/>) and are
/// out of scope for this entity.
/// </summary>
public sealed record InternalUser : BaseEntity
{
    /// <summary>Gets the Windows/legacy user name (e.g. "DOMAIN\username") this row is matched against.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the Entra ID 'oid' (directory object id) claim this user record is matched against.</summary>
    public required Guid? SsoUserIdInt { get; init; }

    /// <summary>Gets a value indicating whether the user can edit any profile.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user has policy-level profile access.</summary>
    public required bool IsPolicyProfileUser { get; init; }
}
