namespace CDC.Api.Domain.Entities;

/// <summary>One user's logged contribution to a profile version section, read from
/// <c>spgProfileVersionSectionUser</c>.</summary>
public sealed record ProfileSectionContribution
{
    /// <summary>Gets the contributing user's identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the contributing user's logon name.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the contributing user's organisation.</summary>
    public string OrganisationName { get; init; } = string.Empty;

    /// <summary>Gets the contributing user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the date the user last contributed to this section.</summary>
    public DateTime LastContributionDate { get; init; }
}
