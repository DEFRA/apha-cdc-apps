namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the species feature. These are
/// the same procedures the legacy <c>Profiles.DataAccess.Sql.SpeciesDataService</c> called.
/// </summary>
public static class SpeciesStoredProcedures
{
    /// <summary>Returns every species and species group.</summary>
    public const string GetAllSpecies = "spgaSpecies";

    /// <summary>Returns the species selected for a named disease filter.</summary>
    public const string GetAllSelectedSpecies = "spgaSelectedDiseaseSpecies";

    /// <summary>Returns sections, questions and fields as three result sets.</summary>
    public const string GetSpeciesMetadata = "spgaSpeciesSectionMetadata";

    /// <summary>Returns the row version, section list and field values as three result sets.</summary>
    public const string GetSpeciesAnswerData = "spgSpeciesAnswerData";

    /// <summary>Checks the row version and returns the new one.</summary>
    public const string UpdateSpeciesAnswerData = "spuSpeciesAnswerData";

    /// <summary>Inserts, updates or deletes a single field value.</summary>
    public const string UpdateSpeciesFieldValue = "spuSpeciesFieldValue";

    /// <summary>Removes every stored value for a multi-value field.</summary>
    public const string DeleteSpeciesFieldMultiValue = "spdSpeciesFieldMultiValue";

    /// <summary>Inserts one value of a multi-value field.</summary>
    public const string InsertSpeciesFieldMultiValue = "spiSpeciesFieldMultiValue";

    /// <summary>Recalculates species prioritisation scores after an answer change.</summary>
    public const string CalculatePrioritisationScore = "sppSpeciesPrioritisationScore";
}
