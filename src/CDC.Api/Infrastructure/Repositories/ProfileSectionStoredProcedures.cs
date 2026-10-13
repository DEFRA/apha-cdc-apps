namespace CDC.Api.Infrastructure.Repositories;

/// <summary>Stored procedure names used by <see cref="ProfileSectionRepository"/>.</summary>
public static class ProfileSectionStoredProcedures
{
    /// <summary>
    /// Reads the full profile questionnaire structure: every section, its questions and each
    /// question's fields. Multi-result-set: sections, questions, fields, field-group fields,
    /// irrelevance links. Only the first three result sets are used.
    /// </summary>
    public const string GetProfileQuestionnaireMetadata = "spgaProfileSectionMetadata";

    /// <summary>
    /// Reads one profile version's recorded answers for one section. Multi-result-set: profiled
    /// species, revision dates, question names, question rows, field values. Only the question
    /// names and field values result sets are used.
    /// </summary>
    public const string GetProfileVersionSection = "spgProfileVersionSection";

    /// <summary>
    /// Clears (or updates) one recorded single-value field answer. Called with no value
    /// parameter, this deletes the matching <c>ProfileVersionFieldValue</c> row.
    /// </summary>
    public const string ClearProfileVersionFieldValue = "spuProfileVersionFieldValue";

    /// <summary>Deletes every recorded value for one "MultiValueList" field.</summary>
    public const string ClearProfileVersionFieldMultiValue = "spdProfileVersionFieldMultiValue";

    /// <summary>Logs a user's contribution to a profile version section.</summary>
    public const string LogProfileVersionSectionContribution = "spiProfileVersionSectionUser";

    /// <summary>
    /// Inserts, updates, or (with every value parameter left NULL) deletes one recorded
    /// single-value field answer. Same stored procedure as <see cref="ClearProfileVersionFieldValue"/>;
    /// named separately here because a save call always supplies a value.
    /// </summary>
    public const string UpsertProfileVersionFieldValue = "spuProfileVersionFieldValue";

    /// <summary>Inserts one recorded value row for a "MultiValueList" field.</summary>
    public const string InsertProfileVersionFieldMultiValue = "spiProfileVersionFieldMultiValue";

    /// <summary>Reads every logged contribution (user, organisation, last contribution date) for
    /// one profile version's section, for the "Contributions" tab.</summary>
    public const string GetProfileVersionSectionContributions = "spgProfileVersionSectionUser";

    /// <summary>Reads every review comment (and reply) for one profile version's section, for the
    /// "Review comments" tab. A recursive query; rows are pre-ordered so each reply immediately
    /// follows its parent.</summary>
    public const string GetProfileSectionReviewComments = "spgReviewCommentByProfileVersionSection";

    /// <summary>Overwrites one section's revision columns (frequencies, next review dates,
    /// statuses and completed dates), checking the row version. Rejects a version that is not a
    /// current draft.</summary>
    public const string UpdateProfileVersionSection = "spuProfileVersionSection";

    /// <summary>Reads the review frequency options (<c>luSectionReviewFrequency</c>).</summary>
    public const string GetSectionReviewFrequencies = "spgaluSectionReviewFrequency";

    /// <summary>Reads the review status options (<c>luReviewStatus</c>).</summary>
    public const string GetSectionReviewStatuses = "spgaluSectionReviewStatusType";
}
