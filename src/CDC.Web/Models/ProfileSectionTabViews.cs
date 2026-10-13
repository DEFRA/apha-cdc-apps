namespace CDC.Web.Models;

/// <summary>One row for the "Contributions" tab: a user's logged contribution to the section.</summary>
public sealed record ProfileSectionContributionView(string FullName, string UserName, string OrganisationName, string LastContributionDate);

/// <summary>One row of the "Review comments" table: a comment (or reply), indented by thread depth.</summary>
/// <param name="Id">The comment id.</param>
/// <param name="Level">The thread depth: 1 for a top-level comment.</param>
/// <param name="DisplaySubject">The subject, truncated for the grid.</param>
/// <param name="Reference">The comment's reference.</param>
/// <param name="Reviewer">The commenting user's full name.</param>
/// <param name="DateDisplay">The comment's date and time as shown in the grid.</param>
/// <param name="IsNew">Whether the comment was made in the last 24 hours (legacy "new!" label).</param>
public sealed record ProfileReviewCommentView(Guid Id, int Level, string DisplaySubject, string Reference, string Reviewer, string DateDisplay, bool IsNew);

/// <summary>The read-only "View review comment" panel.</summary>
/// <param name="Id">The comment id.</param>
/// <param name="Subject">The full subject.</param>
/// <param name="Reference">The reference.</param>
/// <param name="CommentHtml">The comment, sanitised and safe to render as HTML.</param>
/// <param name="CanEdit">Whether the Edit button is shown.</param>
/// <param name="CanDelete">Whether the Delete button is shown.</param>
public sealed record ProfileReviewCommentDetailView(Guid Id, string Subject, string Reference, string CommentHtml, bool CanEdit, bool CanDelete);

/// <summary>Which panel is open beneath the "Review comments" table.</summary>
public enum ReviewCommentPanel
{
    /// <summary>No panel is open.</summary>
    None,

    /// <summary>The read-only "View review comment" panel.</summary>
    View,

    /// <summary>The "Add review comment" panel (a new comment, or a reply).</summary>
    Add,

    /// <summary>The "Edit review comment" panel.</summary>
    Edit
}

/// <summary>The fields posted by the "Add/Edit review comment" panel.</summary>
public sealed class ReviewCommentInput
{
    /// <summary>Gets or sets the subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the reference text.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the comment body (HTML from the link-only rich text editor).</summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>Gets or sets the comment being replied to, when adding a reply.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the comment being edited, when editing.</summary>
    public Guid? CommentId { get; set; }

    /// <summary>Gets or sets the edited comment's row version, base64 encoded.</summary>
    public string LastUpdated { get; set; } = string.Empty;
}
