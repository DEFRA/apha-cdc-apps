namespace CDC.Api.Domain.Entities;

/// <summary>
/// A role a contributor can hold on a profile, as read from <c>spgaluProfileUserRole</c>.
/// </summary>
public sealed record ProfileUserRole
{
    /// <summary>Gets the role identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the role's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets a value indicating whether this role grants section-edit permissions.</summary>
    public required bool IsContributor { get; init; }
}
