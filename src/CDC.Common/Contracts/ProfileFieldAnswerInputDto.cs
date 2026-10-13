namespace CDC.Common.Contracts;

/// <summary>
/// One submitted answer for a profile question field, as posted to
/// <c>POST /api/profile-sections/save</c>. Exactly one of the value properties is populated,
/// matching the field's <c>DataTypeName</c>; all are <see langword="null"/>/empty to clear the
/// field's answer.
/// </summary>
public sealed record ProfileFieldAnswerInputDto
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
}

/// <summary>
/// The result of saving a profile version section's answers, as returned by
/// <c>POST /api/profile-sections/save</c>.
/// </summary>
public sealed record SaveProfileSectionResultDto
{
    /// <summary>Gets the number of field values saved.</summary>
    public int FieldsSavedCount { get; init; }
}
