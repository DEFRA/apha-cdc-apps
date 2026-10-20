namespace CDC.Common.Contracts;

/// <summary>
/// The body of <c>POST /api/profiles/{profileId}/publish</c>, which replaces the legacy "Manage profile" page's
/// "Publish (public)" and "Publish (Defranet only)" links.
/// </summary>
public sealed record PublishProfileRequestDto
{
    /// <summary>Gets a value indicating whether the published version is also made public ("Publish (public)")
    /// rather than visible to DefraNet users only ("Publish (Defranet only)").</summary>
    public bool IsPublic { get; init; }
}

/// <summary>
/// The outcome of <c>POST /api/profiles/{profileId}/publish</c>: either the new published version, or the validation
/// errors that stopped the profile being published. Mirrors the legacy <c>lnkPublish_Click</c> handler, which listed
/// <c>Profile.Validate()</c>'s <c>InvalidReason</c> items instead of publishing when there were any.
/// </summary>
public sealed record PublishProfileResultDto
{
    /// <summary>Gets the identifier of the new published version, or <see langword="null"/> when the profile was not
    /// published because it failed validation.</summary>
    public Guid? NewProfileVersionId { get; init; }

    /// <summary>Gets every validation error that stopped the profile being published, in the order the legacy page listed
    /// them: for each section, the missing "next review" dates, the revision-date errors, then each question's errors.
    /// Empty when the profile was published.</summary>
    public IReadOnlyList<ProfileSectionInvalidReasonDto> InvalidReasons { get; init; } = [];
}
