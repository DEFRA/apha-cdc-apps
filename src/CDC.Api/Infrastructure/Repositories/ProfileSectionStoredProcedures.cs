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
}
