using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// One stored answer for a profile version section. Exactly one value property is populated,
/// determined by the field's data type; a multi-value list field is represented as several rows
/// sharing a field number.
/// </summary>
public sealed record ProfileFieldValue : BaseEntity
{
    /// <summary>Gets the identifier of the question the field belongs to.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public required int FieldNumber { get; init; }

    /// <summary>Gets the answer for a boolean field, or <see langword="null"/>.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference data item for a list field, or <see langword="null"/>.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer for a decimal field, or <see langword="null"/>.</summary>
    public decimal? DecimalValue { get; init; }

    /// <summary>Gets the answer for a date field, or <see langword="null"/>.</summary>
    public DateTime? DateValue { get; init; }

    /// <summary>Gets the answer for a text or long-text (rich/HTML) field, or <see langword="null"/>.</summary>
    public string? TextValue { get; init; }
}
