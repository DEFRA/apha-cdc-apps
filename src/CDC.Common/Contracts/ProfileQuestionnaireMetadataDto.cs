namespace CDC.Common.Contracts;

/// <summary>
/// The profile questionnaire structure: the 16 fixed profile reference sections, their
/// questions, and each question's fields. The structure is the same for every profile; answers
/// are retrieved separately per profile version. Used directly by both CDC.Api's response and
/// CDC.Web's view model (rather than one concrete type per project) because the two never
/// diverge, so a single shared type is all that is needed to keep the wire shape in sync.
/// </summary>
public sealed record ProfileQuestionnaireMetadataDto : QuestionnaireMetadataContract<ProfileSectionMetadataDto>;

/// <summary>A profile reference section (for example "Epidemiology").</summary>
public sealed record ProfileSectionMetadataDto : QuestionnaireSectionMetadataContract<ProfileQuestionMetadataDto>;

/// <summary>A question within a profile reference section.</summary>
public sealed record ProfileQuestionMetadataDto : QuestionnaireQuestionMetadataContract<ProfileFieldMetadataDto>
{
    /// <summary>Gets a value indicating whether the question is answered once per affected species.</summary>
    public bool IsPerSpecies { get; init; }

    /// <summary>Gets a value indicating whether the question allows repeating rows of answers.</summary>
    public bool IsRepeating { get; init; }
}

/// <summary>A single answerable field within a profile question.</summary>
public sealed record ProfileFieldMetadataDto : QuestionnaireFieldMetadataContract;
