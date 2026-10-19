namespace CDC.Common.Contracts;

/// <summary>
/// The result of validating a profile version section, as returned by
/// <c>GET /api/profile-sections/validate</c>. Matches the list of <c>InvalidReason</c> items the
/// legacy <c>ProfileVersionSection.Validate()</c> produced.
/// </summary>
public sealed record ProfileSectionValidationResultDto
{
    /// <summary>Gets a value indicating whether the section has no validation errors.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets every validation error in the order the legacy page listed them: the section's
    /// revision-date errors first, then each question's errors in question order. Empty when
    /// <see cref="IsValid"/>.</summary>
    public IReadOnlyList<ProfileSectionInvalidReasonDto> InvalidReasons { get; init; } = [];
}

/// <summary>One reason a profile version section failed validation, matching the legacy
/// <c>InvalidReason</c>.</summary>
public sealed record ProfileSectionInvalidReasonDto
{
    /// <summary>Gets the question the error belongs to, or <see cref="Guid.Empty"/> for a
    /// section-level (revision dates) error.</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the number of the section the error belongs to.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the number of the question the error belongs to, or 0 for a section-level
    /// (revision dates) error.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the question's short name, which the page's question anchors are built from, or
    /// empty for a section-level (revision dates) error.</summary>
    public string QuestionShortName { get; init; } = string.Empty;

    /// <summary>Gets the error text in the legacy format
    /// <c>[Species][ Row N], "Question - Field" reason</c>, for example
    /// <c>Cattle Row 2, "Laboratory test - Name" is required</c>. May contain HTML markup carried
    /// by a question or species name.</summary>
    public string Description { get; init; } = string.Empty;
}
