namespace CDC.Common.Contracts;

/// <summary>
/// The species questionnaire structure: sections, their questions, and each question's fields.
/// Shared by CDC.Api's response DTO and CDC.Web's view model so the wire shape can never drift
/// between the two. Generic over the concrete section type so each project's nested collection
/// is typed as its own concrete record rather than this base contract.
/// </summary>
public abstract record SpeciesMetadataContract<TSection>
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public IReadOnlyList<TSection> Sections { get; init; } = [];
}

/// <summary>
/// A section of the species questionnaire. Generic over the concrete question/field types so
/// each project's nested collections are typed as its own concrete records rather than this
/// base contract.
/// </summary>
public abstract record SpeciesSectionMetadataContract<TQuestion, TField>
    where TQuestion : SpeciesQuestionMetadataContract<TField>
    where TField : SpeciesFieldMetadataContract
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
    public IReadOnlyList<TQuestion> Questions { get; init; } = [];
}

/// <summary>
/// A question within a questionnaire section. Generic over the concrete field type so each
/// project's nested collection is typed as its own concrete record rather than this base contract.
/// </summary>
public abstract record SpeciesQuestionMetadataContract<TField>
    where TField : SpeciesFieldMetadataContract
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the owning section.</summary>
    public Guid SectionId { get; init; }

    /// <summary>Gets the question text.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the abbreviated question name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the position of the question within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the fields that make up the answer.</summary>
    public IReadOnlyList<TField> Fields { get; init; } = [];
}

/// <summary>A single answerable field within a question.</summary>
public abstract record SpeciesFieldMetadataContract
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

    /// <summary>Gets the data type name, for example <c>Boolean</c>, <c>List</c> or <c>Text</c>.</summary>
    public string DataTypeName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether an answer is required.</summary>
    public bool IsMandatory { get; init; }

    /// <summary>Gets the reference table backing a list field.</summary>
    public Guid ReferenceTableId { get; init; }

    /// <summary>Gets a value indicating whether the backing reference table is user-maintainable.</summary>
    public bool ReferenceTableIsMaintainable { get; init; }

    /// <summary>Gets the editor widget discriminator.</summary>
    public int EditorFieldType { get; init; }
}

