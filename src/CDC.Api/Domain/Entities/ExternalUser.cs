using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A <c>[dbo].[User]</c> row resolved for an authenticated CIDM (external) user. Internal users
/// are resolved through a different mechanism and are out of scope for this entity.
/// </summary>
public sealed record ExternalUser : BaseEntity
{
    /// <summary>Gets the legacy user name (Windows domain\user or SSO user name).</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }

    /// <summary>Gets the email address CIDM authenticated this user with.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Gets the CIDM 'sub' claim (GUID) this user record is matched against.</summary>
    public required Guid CidmSsoId { get; init; }

    /// <summary>
    /// Gets the legacy SSO identity, if any. A non-null value is the existing signal (unchanged
    /// since before CIDM existed) that this row is an external, not internal, user - used to
    /// decide whether a row found by email is allowed to be linked to a CidmSsoId.
    /// </summary>
    public required Guid? SsoUserId { get; init; }

    /// <summary>Gets a value indicating whether the user can edit any profile.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user has policy-level profile access.</summary>
    public required bool IsPolicyProfileUser { get; init; }
}
