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

    /// <summary>Gets the species this value is recorded against, for a per-species question.
    /// <see langword="null"/> otherwise. Not exposed over HTTP - used only to target the exact
    /// row when clearing an answer.</summary>
    public Guid? SpeciesId { get; init; }

    /// <summary>Gets the repeating-question row this value is recorded against. <see
    /// langword="null"/> for a non-repeating question. Not exposed over HTTP - used only to
    /// target the exact row when clearing an answer.</summary>
    public Guid? ProfileVersionQuestionRowId { get; init; }

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
