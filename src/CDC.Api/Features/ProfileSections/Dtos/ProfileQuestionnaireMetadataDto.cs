namespace CDC.Api.Features.ProfileSections.Dtos;

/// <summary>
/// The profile questionnaire structure: the 16 fixed profile reference sections, their
/// questions, and each question's fields. The structure is the same for every profile; answers
/// are retrieved separately per profile version.
/// </summary>
public sealed record ProfileQuestionnaireMetadataDto
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> Sections { get; init; } = [];
}

/// <summary>A profile reference section (for example "Epidemiology").</summary>
public sealed record ProfileSectionMetadataDto
{
    /// <summary>Gets the section identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the section name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the abbreviated section name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the position of the section within the questionnaire.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the questions in this section.</summary>
    public IReadOnlyList<ProfileQuestionMetadataDto> Questions { get; init; } = [];
}

/// <summary>A question within a profile reference section.</summary>
public sealed record ProfileQuestionMetadataDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the owning section.</summary>
    public Guid SectionId { get; init; }

    /// <summary>Gets the abbreviated question name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the position of the question within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets a value indicating whether the question is answered once per affected species.</summary>
    public bool IsPerSpecies { get; init; }

    /// <summary>Gets a value indicating whether the question allows repeating rows of answers.</summary>
    public bool IsRepeating { get; init; }

    /// <summary>Gets the fields that make up the answer.</summary>
    public IReadOnlyList<ProfileFieldMetadataDto> Fields { get; init; } = [];
}

/// <summary>A single answerable field within a profile question.</summary>
public sealed record ProfileFieldMetadataDto
{
    /// <summary>Gets the field identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the owning question.</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the field label.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the abbreviated field name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the position of the field within its question.</summary>
    public int FieldNumber { get; init; }

    /// <summary>Gets the identifier of the field's data type.</summary>
    public Guid DataFieldTypeId { get; init; }

    /// <summary>
    /// Gets the data type name, for example <c>Boolean</c>, <c>List</c>, <c>MultiValueList</c>,
    /// <c>Text</c>, <c>Long Text</c> (rich/HTML content), <c>Decimal</c> or <c>Date</c>.
    /// </summary>
    public string DataTypeName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether an answer is required.</summary>
    public bool IsMandatory { get; init; }

    /// <summary>Gets the reference table backing a list field.</summary>
    public Guid ReferenceTableId { get; init; }

    /// <summary>Gets a value indicating whether the backing reference table is user-maintainable.</summary>
    public bool ReferenceTableIsMaintainable { get; init; }
}
