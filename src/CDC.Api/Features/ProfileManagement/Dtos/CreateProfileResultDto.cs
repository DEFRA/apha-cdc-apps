namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>Outcome of creating a profile. Returned by <c>POST /api/profiles</c>.</summary>
public sealed record CreateProfileResultDto
{
    /// <summary>Gets the new profile's identifier.</summary>
    public Guid NewProfileId { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> created for the new profile.</summary>
    public byte[] NewLastUpdated { get; init; } = [];
}
