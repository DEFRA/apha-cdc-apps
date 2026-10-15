namespace CDC.Common.Contracts;

/// <summary>
/// The profiles an external user can access and the profiles still available to grant, as returned by
/// <c>GET /api/external-users/{id}/profile-access</c>. Shared by CDC.Api and CDC.Web so the wire shape cannot drift.
/// </summary>
public sealed record ExternalUserProfileAccessContract
{
    /// <summary>Gets the profiles the user already has access to.</summary>
    public IReadOnlyList<ExternalUserProfileGrantContract> Profiles { get; init; } = [];

    /// <summary>Gets the profiles the user does not yet have access to.</summary>
    public IReadOnlyList<ProfileOptionContract> AvailableProfiles { get; init; } = [];
}

/// <summary>One profile an external user has access to, with their role and permitted sections.</summary>
public sealed record ExternalUserProfileGrantContract
{
    /// <summary>Gets the profile identifier.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile title, which may contain HTML formatting tags.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the user's role on the profile.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets the display name of the user's role.</summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the role is a contributor role that carries section permissions.</summary>
    public bool IsContributor { get; init; }

    /// <summary>Gets the profile sections the user may edit.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];
}

/// <summary>A profile that can be selected when granting access.</summary>
public sealed record ProfileOptionContract
{
    /// <summary>Gets the profile identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the profile title, which may contain HTML formatting tags.</summary>
    public string Title { get; init; } = string.Empty;
}

/// <summary>Request body for <c>PUT /api/external-users/{id}/profile-access/{profileId}</c>.</summary>
public sealed record SaveExternalUserProfileAccessContract
{
    /// <summary>Gets the role to grant.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets the profile sections to grant. Must be empty for a reviewer role.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];
}
