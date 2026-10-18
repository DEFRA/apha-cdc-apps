namespace CDC.Web.Models;

/// <summary>
/// Outcome of saving or clearing a profile section's answers, or deleting its review comments, through CDC.Api,
/// distinguishing the failure kinds the "Edit profile questions" page reports.
/// </summary>
public enum ProfileSectionWriteOutcome
{
    /// <summary>The change was applied.</summary>
    Success,

    /// <summary>The section was saved by someone else since it was read, or the version is no longer an editable current draft.</summary>
    Conflict,

    /// <summary>The change failed for any other reason.</summary>
    Error
}

/// <summary>Result of saving or clearing a profile section's answers, or deleting its review comments.</summary>
public sealed record ProfileSectionWriteResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required ProfileSectionWriteOutcome Outcome { get; init; }

    /// <summary>Gets the reason to show the user, in the words CDC.Api gave, when the outcome is not <see cref="ProfileSectionWriteOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the number of recorded values saved or cleared when the outcome is <see cref="ProfileSectionWriteOutcome.Success"/>.</summary>
    public int FieldsChangedCount { get; init; }
}
