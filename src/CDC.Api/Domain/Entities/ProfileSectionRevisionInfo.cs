namespace CDC.Api.Domain.Entities;

/// <summary>One profile reference section's technical/policy review dates, read from the revision
/// result set of <c>spgProfileVersionSection</c> (the same stored procedure the Questions tab's
/// answers query uses, which otherwise discards this result set).</summary>
public sealed record ProfileSectionRevisionInfo
{
    public Guid ProfileSectionId { get; init; }

    public int? TechnicalReviewFrequencyMonths { get; init; }

    public DateTime? NextTechnicalReview { get; init; }

    public int? PolicyReviewFrequencyMonths { get; init; }

    public DateTime? NextPolicyReview { get; init; }
}
