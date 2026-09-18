using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// One stored answer. Exactly one of the value properties is populated, determined by the
/// field's data type; a multi-value list field is represented as several rows sharing a
/// field number.
/// </summary>
public sealed record SpeciesFieldValue : BaseEntity
{
    /// <summary>Gets the identifier of the question the field belongs to.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public required int FieldNumber { get; init; }

    /// <summary>Gets the answer for a boolean field, or <see langword="null"/>.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference data item for a list field, or <see langword="null"/>.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer for a text or long-text field, or <see langword="null"/>.</summary>
    public string? TextValue { get; init; }
}
