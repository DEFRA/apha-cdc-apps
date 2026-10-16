namespace CDC.Common.Contracts;

/// <summary>
/// Per-section validation status for one profile version. A section is valid when the legacy
/// <c>ProfileVersionSection.Validate()</c> finds no errors in it, which is what the green tick or red
/// cross on the legacy left-hand navigation showed. Used directly by both CDC.Api's
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

    /// <summary>Gets a value indicating whether the section has no validation errors.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets the questions of the section that have validation errors, which the questions page
    /// marks with a red cross (the legacy <c>question.Validate().Count = 0</c> tick/cross icon).</summary>
    public IReadOnlyList<Guid> InvalidQuestionIds { get; init; } = [];
}
