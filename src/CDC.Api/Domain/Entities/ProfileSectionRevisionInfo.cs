namespace CDC.Api.Domain.Entities;

/// <summary>One profile reference section's technical/policy review dates, read from the revision
/// result set of <c>spgProfileVersionSection</c> (the same stored procedure the Questions tab's
/// answers query uses, which otherwise discards this result set).</summary>
public sealed record ProfileSectionRevisionInfo
{
    /// <summary>Gets the section identifier.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the technical review frequency, in months.</summary>
    public int? TechnicalReviewFrequencyMonths { get; init; }

    /// <summary>Gets the next due technical review date.</summary>
    public DateTime? NextTechnicalReview { get; init; }

    /// <summary>Gets the policy review frequency, in months.</summary>
    public int? PolicyReviewFrequencyMonths { get; init; }

    /// <summary>Gets the next due policy review date.</summary>
    public DateTime? NextPolicyReview { get; init; }
}
