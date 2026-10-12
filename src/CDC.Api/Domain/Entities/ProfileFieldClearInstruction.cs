namespace CDC.Api.Domain.Entities;

/// <summary>
/// One recorded field value to clear, targeting the exact row <c>spuProfileVersionFieldValue</c>/
/// <c>spdProfileVersionFieldMultiValue</c> matches on: field, species (for a per-species question)
/// and question row (for a repeating question).
/// </summary>
public sealed record ProfileFieldClearInstruction
{
    /// <summary>Gets the metadata field identifier (<c>ProfileField.Id</c>) to clear.</summary>
    public required Guid ProfileFieldId { get; init; }

    /// <summary>Gets the species the value is recorded against, for a per-species question.</summary>
    public Guid? SpeciesId { get; init; }

    /// <summary>Gets the repeating-question row the value is recorded against.</summary>
    public Guid? ProfileVersionQuestionRowId { get; init; }

    /// <summary>Gets a value indicating whether the field is a "MultiValueList" (cleared via a
    /// plain delete) rather than a single-value field (cleared via an update with no value).</summary>
    public required bool IsMultiValueList { get; init; }
}
