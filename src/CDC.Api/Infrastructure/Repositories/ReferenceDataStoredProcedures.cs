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

    /// <summary>Lists the reference tables flagged <c>IsMaintainable</c>, for the "maintain reference data" dropdown.</summary>
    public const string GetMaintainableTables = "spgMaintainableReferenceTables";

    /// <summary>Lists one maintainable table's values with their effective dates and whether profiles use them.</summary>
    public const string GetMaintainableValuesByTable = "spgReferenceValueByTable";

    /// <summary>Reads one maintainable value, including the row version needed to change it.</summary>
    public const string GetMaintainableValue = "spgReferenceValue";

    /// <summary>Reads one maintainable table's audit trail, most recent change first.</summary>
    public const string GetAuditTrail = "spgReferenceTableAudit";

    /// <summary>Changes a value's lookup text and writes the audit entry.</summary>
    public const string UpdateValue = "spuReferenceValue";

    /// <summary>Adds a value and writes the audit entry.</summary>
    public const string InsertValue = "spiReferenceValue";

    /// <summary>End-dates a value and writes the audit entry.</summary>
    public const string InactivateValue = "sppReferenceValue";

    /// <summary>Deletes a value and writes the audit entry.</summary>
    public const string DeleteValue = "spdReferenceValue";
}
