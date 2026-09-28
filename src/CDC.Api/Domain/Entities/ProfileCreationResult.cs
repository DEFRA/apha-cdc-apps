namespace CDC.Api.Domain.Entities;

/// <summary>
/// Outcome of creating a profile: its identifier (an echo of the client-supplied identifier)
/// and the row version created for optimistic concurrency, mirroring the legacy
/// <c>CreateProfileResponse</c> data contract.
/// </summary>
public sealed record ProfileCreationResult
{
    /// <summary>Gets the new profile's identifier.</summary>
    public required Guid NewProfileId { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> created for the new profile.</summary>
    public required byte[] NewLastUpdated { get; init; }
}
