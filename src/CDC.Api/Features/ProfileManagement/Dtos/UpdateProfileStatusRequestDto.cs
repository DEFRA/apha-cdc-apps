using System.Text.Json.Serialization;

namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// The body of <c>PUT /api/profiles/{profileId}/status</c>; the profile comes from the route.
/// </summary>
public sealed record UpdateProfileStatusRequestDto
{
    /// <summary>Gets the status to set.</summary>
    [JsonRequired]
    public Guid ProfileStatusId { get; init; }
}
