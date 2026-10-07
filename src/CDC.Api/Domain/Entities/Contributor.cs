namespace CDC.Api.Domain.Entities;

/// <summary>
/// A user who contributes to a profile, as read from <c>spgProfileContributorsByProfileId</c>.
/// </summary>
public sealed record Contributor
{
    /// <summary>Gets the identifier of the user's contributor record on this profile.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the user's full name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }

    /// <summary>Gets the display name of the user's role on this profile.</summary>
    public required string Role { get; init; }

    /// <summary>Gets the row version, used as the concurrency token when removing this contributor.</summary>
    public required byte[] LastUpdated { get; init; }
}
