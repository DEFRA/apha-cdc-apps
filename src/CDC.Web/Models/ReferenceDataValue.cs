namespace CDC.Web.Models;

/// A row of [ReferenceValue], as returned by the legacy spgReferenceValueByTable.
public sealed record ReferenceDataValue(
    Guid Id,
    Guid ReferenceTableId,
    string LookupValue,
    DateOnly? EffectiveDateFrom,
    DateOnly? EffectiveDateTo,
    byte SequenceNumber,
    bool IsInUse,
    byte[]? LastUpdated = null)
{
    /// <summary>Gets a value indicating whether the value is still in force.</summary>
    public bool IsActive => EffectiveDateTo is null;

    /// Gets the user-facing status shown in the reference values table.
    public string Status => IsActive ? "Active" : "Inactive";

    /// Gets the effective date range shown in the reference values table, in the legacy "from - to" form.
    public string EffectiveDates => $"{Format(EffectiveDateFrom)} - {Format(EffectiveDateTo)}".Trim();

    /// Gets the value as shown in the reference values table, marking inactive values as the legacy grid did.
    public string DisplayValue => IsActive ? LookupValue : $"{LookupValue} (Inactive)";

    private static string Format(DateOnly? date) =>
        date?.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
