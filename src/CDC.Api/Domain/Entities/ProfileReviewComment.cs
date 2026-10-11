namespace CDC.Api.Domain.Entities;

/// <summary>One review comment (or reply) for a profile version section, read from the recursive
/// <c>spgReviewCommentByProfileVersionSection</c> query.</summary>
public sealed record ProfileReviewComment
{
    public Guid Id { get; init; }

    public DateTime CommentDate { get; init; }

    public string CommentSubject { get; init; } = string.Empty;

    public string CommentReference { get; init; } = string.Empty;

    public string CommentText { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string UserFullName { get; init; } = string.Empty;

    public Guid ParentId { get; init; }

    public int Level { get; init; }

    public int ChildCount { get; init; }
}
