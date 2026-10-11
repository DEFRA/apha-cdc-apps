namespace CDC.Api.Domain.Entities;

/// <summary>
/// Instructs the repository to write one field's answer for a profile version section, matching
/// the legacy <c>IProfileField.Save()</c> pathway for a standard (non-per-species, non-repeating)
/// question.
/// </summary>
public sealed record ProfileFieldSaveInstruction
{
    /// <summary>Gets the field being answered.</summary>
    public required Guid ProfileFieldId { get; init; }

    /// <summary>Gets the answer for a "Boolean" field.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference data item for a "List" field.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the selected reference data items for a "MultiValueList" field.</summary>
    public IReadOnlyList<Guid> MultiListValues { get; init; } = [];

    /// <summary>Gets the answer for a "Decimal" field.</summary>
    public decimal? DecimalValue { get; init; }

    /// <summary>Gets the answer for a "Date" field.</summary>
    public DateTime? DateValue { get; init; }

    /// <summary>Gets the answer for a "Text"/"Long Text" field.</summary>
    public string? TextValue { get; init; }

    /// <summary>Gets a value indicating whether the field is a "MultiValueList" field, which is
    /// stored as one row per selected value rather than a single upsertable row.</summary>
    public required bool IsMultiValueList { get; init; }
}
