namespace CDC.Api.Domain.Entities;

/// <summary>One review comment (or reply) for a profile version section, read from the recursive
/// <c>spgReviewCommentByProfileVersionSection</c> query.</summary>
public sealed record ProfileReviewComment
{
    /// <summary>Gets the comment identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the date the comment was made.</summary>
    public DateTime CommentDate { get; init; }

    /// <summary>Gets the comment's subject line.</summary>
    public string CommentSubject { get; init; } = string.Empty;

    /// <summary>Gets the comment's optional reference text.</summary>
    public string CommentReference { get; init; } = string.Empty;

    /// <summary>Gets the comment's body text.</summary>
    public string CommentText { get; init; } = string.Empty;

    /// <summary>Gets the commenting user's logon name.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the commenting user's full name.</summary>
    public string UserFullName { get; init; } = string.Empty;

    /// <summary>Gets the parent comment this is a reply to, or <see cref="Guid.Empty"/> for a
    /// top-level comment.</summary>
    public Guid ParentId { get; init; }

    /// <summary>Gets the thread depth: 1 for a top-level comment, 2 for a reply, and so on.</summary>
    public int Level { get; init; }

    /// <summary>Gets the number of direct replies to this comment.</summary>
    public int ChildCount { get; init; }
}
