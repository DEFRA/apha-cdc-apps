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

    /// <summary>Gets the comment's row version, echoed back on edit so a concurrent change is detected.</summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets a value indicating whether the acting user may edit this comment: it is their
    /// own, has no replies, and the version is an editable current draft.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Gets a value indicating whether the acting user may delete this comment (same rule as editing).</summary>
    public bool CanDelete { get; init; }
}

/// <summary>
/// Result of adding or editing a review comment.
/// </summary>
public sealed record SaveProfileReviewCommentResultDto
{
    /// <summary>Gets the saved comment's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the saved comment's new row version.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>The editable text of a review comment.</summary>
/// <param name="Subject">The subject line.</param>
/// <param name="CommentReference">The reference text.</param>
/// <param name="CommentText">The comment body (HTML from the link-only rich text editor).</param>
public sealed record ReviewCommentContent(string Subject, string CommentReference, string CommentText);

/// <summary>
/// One profile reference section's revision (author/VA/policy review) data, as returned by
/// <c>GET /api/profile-sections/all-revision-dates</c>. Sourced from the revision result set of
/// <c>spgProfileVersionSection</c>. The database stores the VA review in its legacy "Technical"
/// columns; the contract uses the "VA" name the UI shows.
/// </summary>
public sealed record ProfileSectionRevisionDatesDto
{
    /// <summary>Gets the section identifier.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the section's display name.</summary>
    public string SectionName { get; init; } = string.Empty;

    /// <summary>Gets the position of the section within the questionnaire.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the author review frequency (a <see cref="ProfileVersionAllRevisionDatesDto.ReviewFrequencies"/> id), or <see langword="null"/> when not set.</summary>
    public Guid? AuthorReviewFrequencyId { get; init; }

    /// <summary>Gets the VA review frequency (a <see cref="ProfileVersionAllRevisionDatesDto.ReviewFrequencies"/> id), or <see langword="null"/> when not set.</summary>
    public Guid? VaReviewFrequencyId { get; init; }

    /// <summary>Gets the policy review frequency (a <see cref="ProfileVersionAllRevisionDatesDto.ReviewFrequencies"/> id), or <see langword="null"/> when not set.</summary>
    public Guid? PolicyReviewFrequencyId { get; init; }

    /// <summary>Gets the next due author review date.</summary>
    public DateTime? NextAuthorReview { get; init; }

    /// <summary>Gets the next due VA review date.</summary>
    public DateTime? NextVaReview { get; init; }

    /// <summary>Gets the next due policy review date.</summary>
    public DateTime? NextPolicyReview { get; init; }

    /// <summary>Gets the author review status (a <see cref="ProfileVersionAllRevisionDatesDto.ReviewStatuses"/> id), or <see langword="null"/> when not set.</summary>
    public Guid? AuthorReviewStatusId { get; init; }

    /// <summary>Gets the VA review status (a <see cref="ProfileVersionAllRevisionDatesDto.ReviewStatuses"/> id), or <see langword="null"/> when not set.</summary>
    public Guid? VaReviewStatusId { get; init; }

    /// <summary>Gets the policy review status (a <see cref="ProfileVersionAllRevisionDatesDto.ReviewStatuses"/> id), or <see langword="null"/> when not set.</summary>
    public Guid? PolicyReviewStatusId { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> to send back when saving, so a concurrent
    /// edit of the section is detected.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>One option of a review frequency or review status drop-down.</summary>
public sealed record ReviewLookupItemDto
{
    /// <summary>Gets the option's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the option's display text (for example "Every year" or "Complete").</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>The "All Revision dates" tab's content for one profile version: every section's
/// revision data in section-number order, plus the options for the frequency and status
/// drop-downs.</summary>
public sealed record ProfileVersionAllRevisionDatesDto
{
    /// <summary>Gets the profile version the revision dates belong to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets each section's revision data, in section-number order.</summary>
    public IReadOnlyList<ProfileSectionRevisionDatesDto> Sections { get; init; } = [];

    /// <summary>Gets the review frequency options (from <c>luSectionReviewFrequency</c>), in sequence order.</summary>
    public IReadOnlyList<ReviewLookupItemDto> ReviewFrequencies { get; init; } = [];

    /// <summary>Gets the review status options (from <c>luReviewStatus</c>), in sequence order.</summary>
    public IReadOnlyList<ReviewLookupItemDto> ReviewStatuses { get; init; } = [];
}

/// <summary>One section's revision data as submitted from the "All Revision dates" tab to
/// <c>PUT /api/profile-sections/all-revision-dates</c>.</summary>
public sealed record ProfileSectionRevisionDatesInputDto
{
    /// <summary>Gets the section being saved.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the author review frequency, or <see langword="null"/> to clear it.</summary>
    public Guid? AuthorReviewFrequencyId { get; init; }

    /// <summary>Gets the VA review frequency, or <see langword="null"/> to clear it.</summary>
    public Guid? VaReviewFrequencyId { get; init; }

    /// <summary>Gets the policy review frequency, or <see langword="null"/> to clear it.</summary>
    public Guid? PolicyReviewFrequencyId { get; init; }

    /// <summary>Gets the next author review date, or <see langword="null"/> to clear it.</summary>
    public DateTime? NextAuthorReview { get; init; }

    /// <summary>Gets the next VA review date, or <see langword="null"/> to clear it.</summary>
    public DateTime? NextVaReview { get; init; }

    /// <summary>Gets the next policy review date, or <see langword="null"/> to clear it.</summary>
    public DateTime? NextPolicyReview { get; init; }

    /// <summary>Gets the author review status, or <see langword="null"/> to clear it.</summary>
    public Guid? AuthorReviewStatusId { get; init; }

    /// <summary>Gets the VA review status, or <see langword="null"/> to clear it.</summary>
    public Guid? VaReviewStatusId { get; init; }

    /// <summary>Gets the policy review status, or <see langword="null"/> to clear it.</summary>
    public Guid? PolicyReviewStatusId { get; init; }

    /// <summary>Gets the row version returned with the section when it was read.</summary>
    public required byte[] LastUpdated { get; init; }
}

/// <summary>The outcome of saving the "All Revision dates" tab.</summary>
public sealed record SaveProfileRevisionDatesResultDto
{
    /// <summary>Gets the number of sections that had changes and were written. Sections whose
    /// submitted values equal what is stored are not written, as in the legacy page.</summary>
    public int SectionsSavedCount { get; init; }
}
