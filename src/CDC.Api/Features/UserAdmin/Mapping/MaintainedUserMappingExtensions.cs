using CDC.Api.Domain.Entities;
using CDC.Api.Features.UserAdmin.Dtos;

namespace CDC.Api.Features.UserAdmin.Mapping;

/// <summary>
/// Maps user administration domain entities onto the DTOs the API returns.
/// </summary>
public static class MaintainedUserMappingExtensions
{
    /// <summary>Maps a <see cref="MaintainedUser"/> onto its DTO.</summary>
    /// <param name="user">The entity to map.</param>
    /// <returns>The mapped DTO.</returns>
    public static MaintainedUserDto ToDto(this MaintainedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new MaintainedUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Organisation = user.Organisation,
            EmailAddress = user.EmailAddress,
            SubscribedToReviewEmails = user.SubscribedToReviewEmails,
            IsExternal = user.IsExternal,
            LastUpdated = user.LastUpdated
        };
    }
}
