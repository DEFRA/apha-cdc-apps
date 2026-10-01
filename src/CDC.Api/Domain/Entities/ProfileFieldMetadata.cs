using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A single answerable field within a profile question, matching the legacy
/// <c>ProfileField</c> data contract.
/// </summary>
public sealed record ProfileFieldMetadata : BaseEntity
{
    /// <summary>Gets the identifier of the owning question.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Gets the field label.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the abbreviated field name.</summary>
    public required string ShortName { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public required int FieldNumber { get; init; }

    /// <summary>Gets the identifier of the field's data type in <c>luDataFieldType</c>.</summary>
    public required Guid DataFieldTypeId { get; init; }

    /// <summary>
    /// Gets the data type name, for example <c>Boolean</c>, <c>List</c>, <c>MultiValueList</c>,
    /// <c>Text</c>, <c>Long Text</c> (rich/HTML content), <c>Decimal</c> or <c>Date</c>.
    /// </summary>
    public required string DataTypeName { get; init; }

    /// <summary>Gets a value indicating whether an answer is required.</summary>
    public required bool IsMandatory { get; init; }

    /// <summary>Gets the reference table backing a list field, or <see cref="Guid.Empty"/> when not a list.</summary>
    public required Guid ReferenceTableId { get; init; }

    /// <summary>Gets a value indicating whether the backing reference table is user-maintainable.</summary>
    public required bool ReferenceTableIsMaintainable { get; init; }
}
