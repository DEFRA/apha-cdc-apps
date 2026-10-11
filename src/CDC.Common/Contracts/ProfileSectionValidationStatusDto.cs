namespace CDC.Common.Contracts;

/// <summary>
/// Per-section validation status for one profile version. A section is valid once every
/// mandatory field across its questions has a recorded answer, mirroring the legacy "section
/// complete" tick/cross shown on the left-hand navigation. Used directly by both CDC.Api's
/// response and CDC.Web's view model (rather than one concrete type per project) because the
/// two never diverge, so a single shared type is all that is needed to keep the wire shape in
/// sync.
/// </summary>
public sealed record ProfileVersionValidationStatusDto
{
    /// <summary>Gets the profile version the status applies to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets each section's validation status.</summary>
    public IReadOnlyList<ProfileSectionValidationStatusDto> Sections { get; init; } = [];
}

/// <summary>Whether one profile reference section has passed validation.</summary>
public sealed record ProfileSectionValidationStatusDto
{
    /// <summary>Gets the section the status applies to.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets a value indicating whether every mandatory field in this section has a recorded answer.</summary>
    public bool IsValid { get; init; }
}
