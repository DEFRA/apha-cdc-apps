namespace CDC.Api.Domain.Common;

/// <summary>
/// Base type for entities identified by a database GUID primary key.
/// </summary>
public abstract record BaseEntity
{
    /// <summary>
    /// Gets the entity's unique identifier, as held in the Surveillance Profiles database.
    /// </summary>
    public required Guid Id { get; init; }
}
