namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>Outcome of updating a profile's attributes. Returned by <c>PUT /api/profiles/{profileId}</c>.</summary>
public sealed record UpdateProfileAttributesResultDto
{
    /// <summary>Gets the new SQL Server <c>rowversion</c>, to use for the next update.</summary>
    public byte[] NewLastUpdated { get; init; } = [];
}
