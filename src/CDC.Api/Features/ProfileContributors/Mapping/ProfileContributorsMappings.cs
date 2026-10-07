using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileContributors.Dtos;

namespace CDC.Api.Features.ProfileContributors.Mapping;

/// <summary>
/// Projects contributor domain entities onto the DTOs returned by the API.
/// </summary>
public static class ProfileContributorsMappings
{
    /// <summary>Projects a contributor entity.</summary>
    /// <param name="contributor">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ContributorDto ToDto(this Contributor contributor) => new()
    {
        Id = contributor.Id,
        UserName = contributor.UserName,
        FullName = contributor.FullName,
        Organisation = contributor.Organisation,
        Role = contributor.Role,
        LastUpdated = contributor.LastUpdated
    };

    /// <summary>Projects a contributor's full editable detail.</summary>
    /// <param name="contributor">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ContributorEditDto ToDto(this ContributorEdit contributor) => new()
    {
        Id = contributor.Id,
        UserName = contributor.UserName,
        FullName = contributor.FullName,
        Organisation = contributor.Organisation,
        RoleId = contributor.RoleId,
        IsSsoUser = contributor.IsSsoUser,
        SectionPermissionIds = contributor.SectionPermissionIds,
        LastUpdated = contributor.LastUpdated
    };

    /// <summary>Projects a contributor role.</summary>
    /// <param name="role">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileUserRoleDto ToDto(this ProfileUserRole role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        IsContributor = role.IsContributor
    };
}
