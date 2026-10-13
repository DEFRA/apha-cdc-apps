namespace CDC.Api.Domain.Entities;

/// <summary>
/// The complete set of revision values to write for one section via <c>spuProfileVersionSection</c>,
/// which overwrites every revision column. The "completed" dates are derived by the service from
/// the legacy rule (set when a status changes to Complete, cleared when it changes away).
/// </summary>
public sealed record ProfileSectionRevisionUpdate
{
    /// <summary>Gets the section being written.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the author review frequency id, or <see langword="null"/> to clear it.</summary>
    public Guid? AuthorReviewFrequencyId { get; init; }

    /// <summary>Gets the VA review frequency id, or <see langword="null"/> to clear it.</summary>
    public Guid? VaReviewFrequencyId { get; init; }

    /// <summary>Gets the policy review frequency id, or <see langword="null"/> to clear it.</summary>
    public Guid? PolicyReviewFrequencyId { get; init; }

    /// <summary>Gets the next author review date, or <see langword="null"/> to clear it.</summary>
    public DateTime? NextAuthorReview { get; init; }

    /// <summary>Gets the next VA review date, or <see langword="null"/> to clear it.</summary>
    public DateTime? NextVaReview { get; init; }

    /// <summary>Gets the next policy review date, or <see langword="null"/> to clear it.</summary>
    public DateTime? NextPolicyReview { get; init; }

    /// <summary>Gets the author review status id, or <see langword="null"/> to clear it.</summary>
    public Guid? AuthorReviewStatusId { get; init; }

    /// <summary>Gets the VA review status id, or <see langword="null"/> to clear it.</summary>
    public Guid? VaReviewStatusId { get; init; }

    /// <summary>Gets the policy review status id, or <see langword="null"/> to clear it.</summary>
    public Guid? PolicyReviewStatusId { get; init; }

    /// <summary>Gets the date the author review was completed, or <see langword="null"/>.</summary>
    public DateTime? AuthorReviewCompleted { get; init; }

    /// <summary>Gets the date the VA review was completed, or <see langword="null"/>.</summary>
    public DateTime? VaReviewCompleted { get; init; }

    /// <summary>Gets the date the policy review was completed, or <see langword="null"/>.</summary>
    public DateTime? PolicyReviewCompleted { get; init; }

    /// <summary>Gets the row version read with the section, checked by the procedure so a
    /// concurrent edit is rejected.</summary>
    public required byte[] LastUpdated { get; init; }
}
