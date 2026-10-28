using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// The signed-in user an API request is made for: their <c>[dbo].[User]</c> row id, the role flags and sign-on
/// identifiers held on it, and the roles they hold on individual profiles (<c>spgActingUser</c>).
/// </summary>
public sealed record ActingUser : BaseEntity
{
    /// <summary>Gets a value indicating whether the row's profile editor flag is set. Only an internal user's flag counts.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the row's policy profile user flag is set. Only an internal user's flag counts.</summary>
    public required bool IsPolicyProfileUser { get; init; }

    /// <summary>Gets the Entra ID object id that marks the user as internal, or <see langword="null"/>.</summary>
    public Guid? SsoUserIdInt { get; init; }

    /// <summary>Gets the CIDM identifier that marks the user as external, or <see langword="null"/>.</summary>
    public Guid? SsoUserIdExt { get; init; }

    /// <summary>Gets the roles the user holds on profiles. Only an external user's roles count.</summary>
    public IReadOnlyList<ProfileRoleAssignment> ProfileRoles { get; init; } = [];

    /// <summary>Gets the sections the user is assigned to, across every profile. Only an external author's assignments count.</summary>
    public IReadOnlyList<ProfileSectionAssignment> SectionAssignments { get; init; } = [];

    /// <summary>Gets the kind of user, decided by which sign-on identifier the row holds.</summary>
    public UserKind Kind => (SsoUserIdInt, SsoUserIdExt) switch
    {
        (not null, null) => UserKind.Internal,
        (null, not null) => UserKind.External,
        _ => UserKind.Unclassified
    };
}
