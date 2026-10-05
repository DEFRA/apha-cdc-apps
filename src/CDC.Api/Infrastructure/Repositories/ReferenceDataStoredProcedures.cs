namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the reference data feature.
/// </summary>
public static class ReferenceDataStoredProcedures
{
    /// <summary>
    /// Returns every value in a reference table, for populating a question's selectable options.
    /// Unlike <c>spgReferenceValueByTable</c> (used by the "maintain reference data" admin screen),
    /// this works for every reference table, not only ones flagged as user-maintainable.
    /// </summary>
    public const string GetLookupValuesByTable = "spgLookupValueByTable";
}
