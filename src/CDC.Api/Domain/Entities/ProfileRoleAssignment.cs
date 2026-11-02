namespace CDC.Api.Domain.Entities;

/// <summary>
/// The role a user holds on one profile, as read from <c>spgActingUser</c>. The six roles are technical,
/// policy and VA, each as an author (who edits the sections they are assigned) or a reviewer (who comments but does not edit).
/// </summary>
/// <param name="ProfileId">The profile the role is held on.</param>
/// <param name="RoleId">The role's identifier in <c>luProfileUserRole</c>.</param>
/// <param name="RoleName">The role's name, for example "Technical author".</param>
/// <param name="IsContributor">Whether the role is an author role rather than a reviewer role.</param>
public sealed record ProfileRoleAssignment(Guid ProfileId, Guid RoleId, string RoleName, bool IsContributor)
{
    /// <summary>Gets the review this role is responsible for, or <see langword="null"/> for a role the catalogue does not know.</summary>
    public ReviewType? ReviewType => ProfileRoleCatalog.ReviewTypeOf(RoleName);
}
