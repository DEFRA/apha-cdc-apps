namespace CDC.Web.Models;

/// <summary>
/// Outcome of adding, editing or deleting a review comment through CDC.Api, distinguishing the
/// failure kinds the "Review comments" tab reports differently.
/// </summary>
public enum ReviewCommentOutcome
{
    /// <summary>The change was applied.</summary>
    Success,

    /// <summary>CDC.Api rejected the submission as invalid.</summary>
    ValidationFailed,

    /// <summary>The comment can no longer be changed by this user (not their own, it has replies, or the version is not an editable draft).</summary>
    Forbidden,

    /// <summary>The comment (or the comment being replied to) no longer exists.</summary>
    NotFound,

    /// <summary>The comment was edited by someone else.</summary>
    Conflict,

    /// <summary>The change failed for any other reason.</summary>
    Error
}

/// <summary>Result of adding, editing or deleting a review comment.</summary>
public sealed record ReviewCommentResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required ReviewCommentOutcome Outcome { get; init; }

    /// <summary>Gets the reason to show the user when the outcome is not <see cref="ReviewCommentOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
