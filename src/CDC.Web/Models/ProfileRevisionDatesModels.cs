namespace CDC.Web.Models;

/// <summary>
/// Outcome of a call to <c>PUT /api/profile-sections/all-revision-dates</c>, distinguishing the
/// failure kinds the "All Revision dates" tab reacts to differently.
/// </summary>
public enum SaveRevisionDatesOutcome
{
    /// <summary>The revision dates were saved.</summary>
    Success,

    /// <summary>CDC.Api rejected the submission as invalid.</summary>
    ValidationFailed,

    /// <summary>A section was edited by someone else, or the version is no longer an editable draft.</summary>
    Conflict,

    /// <summary>The save failed for any other reason.</summary>
    Error
}

/// <summary>Result of a call to <c>PUT /api/profile-sections/all-revision-dates</c>.</summary>
public sealed record SaveRevisionDatesResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SaveRevisionDatesOutcome Outcome { get; init; }

    /// <summary>Gets the message to show the user, if any.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the number of sections written when <see cref="Outcome"/> is <see cref="SaveRevisionDatesOutcome.Success"/>.</summary>
    public int SectionsSavedCount { get; init; }
}

/// <summary>One option of a frequency or status drop-down on the "All Revision dates" tab.</summary>
/// <param name="Value">The option's id, as posted back (empty for the blank option).</param>
/// <param name="Text">The option's display text.</param>
public sealed record RevisionDatesOption(string Value, string Text);

/// <summary>
/// One section's row on the editable "All Revision dates" tab. Every value is held as the text
/// shown in (or posted from) its control, so a rejected submission re-renders exactly what the
/// user typed.
/// </summary>
public sealed record RevisionDatesRowView
{
    /// <summary>Gets the section's identifier.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the section's display text, for example "1 Summary".</summary>
    public string SectionDisplay { get; init; } = string.Empty;

    /// <summary>Gets the selected author review frequency id, or empty for none.</summary>
    public string AuthorFrequency { get; init; } = string.Empty;

    /// <summary>Gets the selected VA review frequency id, or empty for none.</summary>
    public string VaFrequency { get; init; } = string.Empty;

    /// <summary>Gets the selected policy review frequency id, or empty for none.</summary>
    public string PolicyFrequency { get; init; } = string.Empty;

    /// <summary>Gets the next author review date text.</summary>
    public string NextAuthorReview { get; init; } = string.Empty;

    /// <summary>Gets the next VA review date text.</summary>
    public string NextVaReview { get; init; } = string.Empty;

    /// <summary>Gets the next policy review date text.</summary>
    public string NextPolicyReview { get; init; } = string.Empty;

    /// <summary>Gets the selected author review status id, or empty for none.</summary>
    public string AuthorStatus { get; init; } = string.Empty;

    /// <summary>Gets the selected VA review status id, or empty for none.</summary>
    public string VaStatus { get; init; } = string.Empty;

    /// <summary>Gets the selected policy review status id, or empty for none.</summary>
    public string PolicyStatus { get; init; } = string.Empty;

    /// <summary>Gets the section's row version (base64) as read, posted back to detect a concurrent edit.</summary>
    public string LastUpdated { get; init; } = string.Empty;

    /// <summary>Gets the validation messages for this row from the last submission.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>The "Set For All Sections" values: a status and a next review date per review type.
/// They are copied into every row by <c>revision-dates.js</c> and are not themselves saved.</summary>
public sealed record RevisionDatesSetForAllView
{
    /// <summary>Gets the author review status id, or empty for none.</summary>
    public string AuthorStatus { get; init; } = string.Empty;

    /// <summary>Gets the next author review date text.</summary>
    public string NextAuthorReview { get; init; } = string.Empty;

    /// <summary>Gets the VA review status id, or empty for none.</summary>
    public string VaStatus { get; init; } = string.Empty;

    /// <summary>Gets the next VA review date text.</summary>
    public string NextVaReview { get; init; } = string.Empty;

    /// <summary>Gets the policy review status id, or empty for none.</summary>
    public string PolicyStatus { get; init; } = string.Empty;

    /// <summary>Gets the next policy review date text.</summary>
    public string NextPolicyReview { get; init; } = string.Empty;
}

/// <summary>Which of the three reviews a user may change on the "All Revision dates" tab. A profile editor may change all of
/// them; an author only the review their role owns: technical for the author review, policy, or VA.</summary>
/// <param name="Author">Whether the author (technical) review can be changed.</param>
/// <param name="Va">Whether the VA review can be changed.</param>
/// <param name="Policy">Whether the policy review can be changed.</param>
public readonly record struct EditableReviews(bool Author, bool Va, bool Policy)
{
    /// <summary>Gets the editability when every review can be changed.</summary>
    public static EditableReviews All { get; } = new(true, true, true);

    /// <summary>Gets a value indicating whether any review can be changed.</summary>
    public bool Any => Author || Va || Policy;
}

/// <summary>Everything the "All Revision dates" tab renders.</summary>
/// <param name="Rows">One row per section, in section-number order.</param>
/// <param name="FrequencyOptions">The review frequency drop-down options (blank option first).</param>
/// <param name="StatusOptions">The review status drop-down options (blank option first).</param>
/// <param name="SetForAll">The "Set For All Sections" control values.</param>
/// <param name="IsEditable">Whether the tab is editable (and shows Save), or read-only.</param>
public sealed record RevisionDatesTabView(
    IReadOnlyList<RevisionDatesRowView> Rows,
    IReadOnlyList<RevisionDatesOption> FrequencyOptions,
    IReadOnlyList<RevisionDatesOption> StatusOptions,
    RevisionDatesSetForAllView SetForAll,
    bool IsEditable)
{
    /// <summary>Gets which reviews can be changed. An editable tab lets every review be changed unless a smaller set is given;
    /// a read-only tab lets none.</summary>
    public EditableReviews Editable { get; init; } = IsEditable ? EditableReviews.All : default;
}
