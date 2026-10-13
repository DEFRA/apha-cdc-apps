namespace CDC.Api.Domain.Entities;

/// <summary>One profile reference section's author/VA/policy review data, read from the revision
/// result set of <c>spgProfileVersionSection</c>. The database stores the VA review in its legacy
/// "Technical" columns.</summary>
public sealed record ProfileSectionRevisionInfo
{
    /// <summary>Gets the section identifier.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the author review frequency id, or <see langword="null"/> when not set.</summary>
    public Guid? AuthorReviewFrequencyId { get; init; }

    /// <summary>Gets the VA review frequency id, or <see langword="null"/> when not set.</summary>
    public Guid? VaReviewFrequencyId { get; init; }

    /// <summary>Gets the policy review frequency id, or <see langword="null"/> when not set.</summary>
    public Guid? PolicyReviewFrequencyId { get; init; }

    /// <summary>Gets the next due author review date.</summary>
    public DateTime? NextAuthorReview { get; init; }

    /// <summary>Gets the next due VA review date.</summary>
    public DateTime? NextVaReview { get; init; }

    /// <summary>Gets the next due policy review date.</summary>
    public DateTime? NextPolicyReview { get; init; }

    /// <summary>Gets the author review status id, or <see langword="null"/> when not set.</summary>
    public Guid? AuthorReviewStatusId { get; init; }

    /// <summary>Gets the VA review status id, or <see langword="null"/> when not set.</summary>
    public Guid? VaReviewStatusId { get; init; }

    /// <summary>Gets the policy review status id, or <see langword="null"/> when not set.</summary>
    public Guid? PolicyReviewStatusId { get; init; }

    /// <summary>Gets the row version, sent back when updating so a concurrent edit is detected.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
