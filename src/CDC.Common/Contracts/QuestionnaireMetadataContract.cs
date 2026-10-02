namespace CDC.Common.Contracts;

/// <summary>
/// The structure of a questionnaire (species or profile): its sections, ordered by section
/// number. Shared base for both questionnaires so the common section/question/field property
/// groups are declared once rather than repeated per questionnaire.
/// </summary>
public abstract record QuestionnaireMetadataContract<TSection>
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public IReadOnlyList<TSection> Sections { get; init; } = [];
}

/// <summary>
/// A section of a questionnaire (for example "Epidemiology"). Generic over the concrete question
/// type so each questionnaire's nested collection is typed as its own concrete record rather than
/// this base contract.
/// </summary>
public abstract record QuestionnaireSectionMetadataContract<TQuestion>
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
/// questionnaire's nested collection is typed as its own concrete record rather than this base
/// contract.
/// </summary>
public abstract record QuestionnaireQuestionMetadataContract<TField>
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the owning section.</summary>
    public Guid SectionId { get; init; }

    /// <summary>Gets the abbreviated question name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the position of the question within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the fields that make up the answer.</summary>
    public IReadOnlyList<TField> Fields { get; init; } = [];
}

/// <summary>A single answerable field within a questionnaire question.</summary>
public abstract record QuestionnaireFieldMetadataContract
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

/// <summary>One stored answer for a questionnaire field. Exactly one value property is populated, per the field's data type.</summary>
public abstract record QuestionnaireFieldValueContract
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

    /// <summary>Gets the answer for a text or long-text (rich/HTML) field.</summary>
    public string? TextValue { get; init; }
}
