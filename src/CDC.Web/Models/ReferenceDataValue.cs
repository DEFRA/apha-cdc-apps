namespace CDC.Web.Models;

/// A row of [ReferenceValue], as returned by the legacy spgReferenceValueByTable.
public sealed record ReferenceDataValue(
    Guid Id,
    Guid ReferenceTableId,
    string LookupValue,
    DateOnly? EffectiveDateFrom,
    DateOnly? EffectiveDateTo,
    byte SequenceNumber,
    bool IsInUse)
{
    /// <summary>Gets a value indicating whether the value is still in force.</summary>
    public bool IsActive => EffectiveDateTo is null;

    /// Gets the user-facing status shown in the reference values table.
    public string Status => IsActive ? "Active" : "Inactive";
}
