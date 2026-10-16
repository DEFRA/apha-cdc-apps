namespace CDC.Api.Domain.Entities;

/// <summary>The profiles an external user can access, and those still available to grant, from <c>spgaSsoUserMaintenance</c>.</summary>
public sealed record ExternalUserProfileAccess
{
    /// <summary>Gets the profiles the user already has access to.</summary>
    public required IReadOnlyList<ExternalUserProfileGrant> Profiles { get; init; }

    /// <summary>Gets the profiles the user does not yet have access to.</summary>
    public required IReadOnlyList<ProfileOption> AvailableProfiles { get; init; }
}

/// <summary>One profile an external user has access to.</summary>
public sealed record ExternalUserProfileGrant
{
    /// <summary>Gets the profile identifier.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Gets the profile title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the user's role identifier on the profile.</summary>
    public required Guid RoleId { get; init; }

    /// <summary>Gets the display name of the role.</summary>
    public required string RoleName { get; init; }

    /// <summary>Gets a value indicating whether the role carries section permissions.</summary>
    public required bool IsContributor { get; init; }

    /// <summary>Gets the profile sections the user may edit.</summary>
    public required IReadOnlyList<Guid> SectionPermissionIds { get; init; }
}

/// <summary>A profile that can be selected when granting access.</summary>
public sealed record ProfileOption
{
    /// <summary>Gets the profile identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the profile title.</summary>
    public required string Title { get; init; }
}
