using CDC.Api.Domain.Entities;

namespace CDC.Api.Application;

/// <summary>What the profile-level access rules need to know about one profile version.</summary>
/// <param name="ProfileId">The profile the version belongs to.</param>
/// <param name="IsPublished">Whether the version is Published rather than Draft.</param>
/// <param name="IsCurrent">Whether the version is current, that is, has no effective-to date.</param>
public readonly record struct ProfileVersionFacts(Guid ProfileId, bool IsPublished, bool IsCurrent)
{
    /// <summary>Gets a value indicating whether this is the profile's current draft, the only version that can be edited.</summary>
    public bool IsCurrentDraft => IsCurrent && !IsPublished;

    /// <summary>Reads the facts from a version header.</summary>
    /// <param name="header">The version's header.</param>
    /// <returns>The facts.</returns>
    public static ProfileVersionFacts From(ProfileVersionHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        return new ProfileVersionFacts(header.ProfileId, header.IsPublished, header.IsCurrent);
    }

    /// <summary>Reads the facts from a version summary, which does not carry the profile it belongs to.</summary>
    /// <param name="profileId">The profile the version belongs to.</param>
    /// <param name="summary">The version's summary.</param>
    /// <returns>The facts.</returns>
    public static ProfileVersionFacts From(Guid profileId, ProfileVersionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProfileVersionFacts(profileId, summary.IsPublished, summary.EffectiveDateTo is null);
    }
}
