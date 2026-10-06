namespace CDC.Common.Contracts;

/// <summary>
/// The species questionnaire structure: sections, their questions, and each question's fields.
/// Shared by CDC.Api's response DTO and CDC.Web's view model so the wire shape can never drift
/// between the two. The common section/question/field property groups live on
/// <see cref="QuestionnaireMetadataContract{TSection}"/> and friends; only the properties unique
/// to the species questionnaire are declared here.
/// </summary>
public abstract record SpeciesMetadataContract<TSection> : QuestionnaireMetadataContract<TSection>;

/// <summary>
/// A section of the species questionnaire. Generic over the concrete question/field types so
/// each project's nested collections are typed as its own concrete records rather than this
/// base contract.
/// </summary>
public abstract record SpeciesSectionMetadataContract<TQuestion, TField> : QuestionnaireSectionMetadataContract<TQuestion>
    where TQuestion : SpeciesQuestionMetadataContract<TField>
    where TField : SpeciesFieldMetadataContract;

/// <summary>
/// A question within a questionnaire section. Generic over the concrete field type so each
/// project's nested collection is typed as its own concrete record rather than this base contract.
/// </summary>
public abstract record SpeciesQuestionMetadataContract<TField> : QuestionnaireQuestionMetadataContract<TField>
    where TField : SpeciesFieldMetadataContract
{
    /// <summary>Gets the question text.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>A single answerable field within a question.</summary>
public abstract record SpeciesFieldMetadataContract : QuestionnaireFieldMetadataContract
{
    /// <summary>Gets the editor widget discriminator.</summary>
    public int EditorFieldType { get; init; }
}

