namespace CDC.Api.Features.ProfileSearch.Dtos;

/// <summary>
/// Represents one version of a surveillance profile.
/// </summary>
public sealed record ProfileVersionDto
{
    /// <summary>Gets or sets the profile version identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets or sets the parent profile identifier.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Gets or sets the version number.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>Gets or sets the version title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets or sets the version content.</summary>
    public required string Content { get; init; }

    /// <summary>Gets or sets a value indicating whether the version is published.</summary>
    public required bool IsPublished { get; init; }

    /// <summary>Gets or sets the created timestamp.</summary>
    public required DateTime CreatedAtUtc { get; init; }
}
