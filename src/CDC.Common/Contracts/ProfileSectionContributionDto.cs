namespace CDC.Common.Contracts;

/// <summary>
/// One user's logged contribution to a profile version section, as returned by
/// <c>GET /api/profile-sections/contributions</c>. Sourced from <c>spgProfileVersionSectionUser</c>,
/// the same table the Clear/Save/Validate pathways already write to via
/// <c>spiProfileVersionSectionUser</c>.
/// </summary>
public sealed record ProfileSectionContributionDto
{
    /// <summary>Gets the contributing user's identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the contributing user's logon name.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the contributing user's organisation.</summary>
    public string OrganisationName { get; init; } = string.Empty;

    /// <summary>Gets the contributing user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the date the user last contributed to this section.</summary>
    public DateTime LastContributionDate { get; init; }
}

/// <summary>
/// One review comment (or reply) for a profile version section, as returned by
/// <c>GET /api/profile-sections/review-comments</c>. Sourced from the recursive
/// <c>spgReviewCommentByProfileVersionSection</c> query; rows are already ordered so that each
/// reply immediately follows its parent, matching the legacy threaded display.
/// </summary>
public sealed record ProfileReviewCommentDto
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

/// <summary>
/// One profile reference section's revision (technical/policy review) dates, as returned by
/// <c>GET /api/profile-sections/all-revision-dates</c>. Sourced from the revision result set of
/// <c>spgProfileVersionSection</c>, which the Questions tab's answers query already reads but
/// discards.
/// </summary>
public sealed record ProfileSectionRevisionDatesDto
{
    /// <summary>Gets the section identifier.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the section's display name.</summary>
    public string SectionName { get; init; } = string.Empty;

    /// <summary>Gets the position of the section within the questionnaire.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the technical review frequency, in months.</summary>
    public int? TechnicalReviewFrequencyMonths { get; init; }

    /// <summary>Gets the next due technical review date.</summary>
    public DateTime? NextTechnicalReview { get; init; }

    /// <summary>Gets the policy review frequency, in months.</summary>
    public int? PolicyReviewFrequencyMonths { get; init; }

    /// <summary>Gets the next due policy review date.</summary>
    public DateTime? NextPolicyReview { get; init; }
}

/// <summary>The "All Revision dates" tab's content for one profile version: every section's
/// revision dates, in section-number order.</summary>
public sealed record ProfileVersionAllRevisionDatesDto
{
    /// <summary>Gets the profile version the revision dates belong to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets each section's revision dates, in section-number order.</summary>
    public IReadOnlyList<ProfileSectionRevisionDatesDto> Sections { get; init; } = [];
}
