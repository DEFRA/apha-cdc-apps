namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile reports feature.
/// </summary>
public static class ProfileReportStoredProcedures
{
    /// <summary>Reads the reports available for a profile version.</summary>
    public const string GetProfileVersionReports = "spgProfileVersionReportByProfileVersionId";

    /// <summary>Reads a previously persisted report document.</summary>
    public const string GetProfileVersionReportData = "spgProfileVersionReportData";

    /// <summary>Persists a generated report document.</summary>
    public const string InsertProfileVersionReportData = "spiProfileVersionReportData";

    /// <summary>Reads a profile's version history in one state (draft or published); the first result set lists the versions.</summary>
    public const string GetProfileHistory = "spgProfileHistory";

    /// <summary>Reads one profile version's title, number, state and dates; the first result set holds the version.</summary>
    public const string GetProfileVersionInfoById = "spgProfileVersionInfoById";

    /// <summary>Reads the questionnaire: sections (with notes), questions (with relevance rules), fields and field-group child fields.</summary>
    public const string GetQuestionnaireMetadata = "spgaProfileSectionMetadata";

    /// <summary>Reads one profile version section: profiled species, revision dates, question names, answer rows and field values.</summary>
    public const string GetProfileVersionSection = "spgProfileVersionSection";

    /// <summary>Reads a maintainable reference table's values, including which are inactive.</summary>
    public const string GetReferenceValuesByTable = "spgReferenceValueByTable";

    /// <summary>Reads any reference table's values, for tables that are not user-maintainable.</summary>
    public const string GetLookupValuesByTable = "spgLookupValueByTable";

    /// <summary>Reads each species' score and rank per prioritisation category for a profile version.</summary>
    public const string GetPrioritisationCategoryScores = "spgPrioritisationCategoryScoreByProfileVersionId";

    /// <summary>Reads each species' value and score per prioritisation criterion for a profile version.</summary>
    public const string GetPrioritisationCriterionScores = "spgPrioritisationCriterionScoreByProfileVersionId";

    /// <summary>Reads the species that were prioritised for a profile version.</summary>
    public const string GetPrioritisedSpecies = "spgPrioritisedSpeciesByProfileVersionId";

    /// <summary>Rebuilds one profile version's species prioritisation criteria from its answers.</summary>
    public const string CalculatePrioritisation = "sppPrioritisationCalculation";

    /// <summary>Rebuilds the category scores and ranks of every profile version from the criteria; takes no parameters.</summary>
    public const string CalculatePrioritisationScore = "sppPrioritisationScore";

    /// <summary>Reads the profile sections whose reviews fall due in a date range for one user, for the disease reviews report.</summary>
    public const string GetDiseaseReviews = "spgaDiseaseReviews";
}
