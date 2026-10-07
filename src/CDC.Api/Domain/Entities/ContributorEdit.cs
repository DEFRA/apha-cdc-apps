namespace CDC.Api.Domain.Entities;

/// <summary>
/// Full editable detail for one profile contributor, as read from <c>spgContributor</c>.
/// </summary>
public sealed record ContributorEdit
{
    /// <summary>Gets the identifier of the user's contributor record on this profile.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the user's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }

    /// <summary>Gets the identifier of the contributor's current role on this profile.</summary>
    public required Guid RoleId { get; init; }

    /// <summary>Gets a value indicating whether the user signs in via SSO.</summary>
    public required bool IsSsoUser { get; init; }

    /// <summary>Gets the profile sections the contributor currently has permission to edit.</summary>
    public required IReadOnlyList<Guid> SectionPermissionIds { get; init; }

    /// <summary>Gets the row version, used for optimistic concurrency on update.</summary>
    public required byte[] LastUpdated { get; init; }
}
