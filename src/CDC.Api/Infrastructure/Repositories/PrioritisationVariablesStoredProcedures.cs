namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the prioritisation variables
/// feature.
/// </summary>
public static class PrioritisationVariablesStoredProcedures
{
    /// <summary>Returns ranking range, categories, criteria and criterion values as four result sets.</summary>
    public const string GetAll = "spgaPrioritisationVariables";

    /// <summary>Updates a criterion's name and weighting.</summary>
    public const string UpdateCriterion = "spuPrioritisationCriterion";

    /// <summary>Updates a criterion value's score.</summary>
    public const string UpdateCriterionValue = "spuPrioritisationCriterionValue";

    /// <summary>Updates the ranking range, checking the row version.</summary>
    public const string UpdateRankingRange = "spuPrioritisationRankingRange";
}
