namespace CDC.Common.Contracts;

/// <summary>
/// Full editable detail for one profile contributor, as returned by
/// <c>GET /api/profiles/{profileId}/contributors/{contributorId}</c>. Shared by CDC.Api's
/// response DTO and CDC.Web's view model so the wire shape can never drift between the two.
/// </summary>
public abstract record ContributorEditContract
{
    /// <summary>Gets the identifier of the user's contributor record on this profile.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the identifier of the contributor's current role on this profile.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets a value indicating whether the user signs in via SSO, in which case
    /// <see cref="UserName"/>, <see cref="FullName"/> and <see cref="Organisation"/> are
    /// read-only - they are maintained centrally, not per profile.</summary>
    public bool IsSsoUser { get; init; }

    /// <summary>Gets the profile sections the contributor currently has permission to edit.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];

    /// <summary>Gets the row version, used for optimistic concurrency on update.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
