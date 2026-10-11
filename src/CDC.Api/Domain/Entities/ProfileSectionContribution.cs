namespace CDC.Api.Domain.Entities;

/// <summary>One user's logged contribution to a profile version section, read from
/// <c>spgProfileVersionSectionUser</c>.</summary>
public sealed record ProfileSectionContribution
{
    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string OrganisationName { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public DateTime LastContributionDate { get; init; }
}
