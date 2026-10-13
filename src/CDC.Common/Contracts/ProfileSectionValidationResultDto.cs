namespace CDC.Common.Contracts;

/// <summary>
/// The result of validating a profile version section, as returned by
/// <c>GET /api/profile-sections/validate</c>. Matches the legacy
/// <c>ProfileVersionSection.Validate()</c> "invalid reasons" list, scoped to mandatory,
/// unanswered questions.
/// </summary>
public sealed record ProfileSectionValidationResultDto
{
    /// <summary>Gets a value indicating whether every mandatory field in the section has a
    /// recorded answer.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets the mandatory questions with no recorded answer, formatted as
    /// "{section number}.{question number} {question name}". Empty when <see cref="IsValid"/>.</summary>
    public IReadOnlyList<string> UnansweredQuestions { get; init; } = [];
}
