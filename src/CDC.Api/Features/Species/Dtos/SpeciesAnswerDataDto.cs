namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// All recorded answers for a single species.
/// </summary>
public sealed record SpeciesAnswerDataDto
{
    /// <summary>Gets the species the answers belong to.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>Gets the species display name.</summary>
    public string SpeciesName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the row version of the species record, base64 encoded. Send it back unchanged on
    /// update; a mismatch means another user has saved and the update is rejected with 409.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the answers grouped by questionnaire section.</summary>
    public IReadOnlyList<SpeciesSectionDto> Sections { get; init; } = [];
}

/// <summary>
/// The answered field values for one questionnaire section.
/// </summary>
public sealed record SpeciesSectionDto
{
    /// <summary>Gets the identifier of the section these values belong to.</summary>
    public Guid SectionId { get; init; }

    /// <summary>Gets the recorded field values.</summary>
    public IReadOnlyList<SpeciesFieldValueDto> FieldValues { get; init; } = [];
}

/// <summary>
/// One stored answer. Exactly one value property is populated, per the field's data type.
/// </summary>
public sealed record SpeciesFieldValueDto
{
    /// <summary>Gets the field value identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the question the field belongs to.</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public int FieldNumber { get; init; }

    /// <summary>Gets the answer for a boolean field.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference data item for a list field.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer for a text or long-text field.</summary>
    public string? TextValue { get; init; }
}
