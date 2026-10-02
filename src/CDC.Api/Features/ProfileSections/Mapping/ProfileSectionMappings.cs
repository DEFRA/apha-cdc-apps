using CDC.Api.Domain.Entities;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileSections.Mapping;

/// <summary>
/// Projects profile section domain entities onto the DTOs returned by the API.
/// </summary>
public static class ProfileSectionMappings
{
    /// <summary>Projects the questionnaire metadata hierarchy.</summary>
    /// <param name="metadata">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileQuestionnaireMetadataDto ToDto(this ProfileQuestionnaireMetadata metadata) => new()
    {
        Sections = [.. metadata.Sections.Select(ToDto)]
    };

    /// <summary>Projects a profile reference section.</summary>
    /// <param name="section">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileSectionMetadataDto ToDto(this ProfileSectionMetadata section) => new()
    {
        Id = section.Id,
        Name = section.Name,
        ShortName = section.ShortName,
        SectionNumber = section.SectionNumber,
        Questions = [.. section.Questions.Select(ToDto)]
    };

    /// <summary>Projects a profile question.</summary>
    /// <param name="question">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileQuestionMetadataDto ToDto(this ProfileQuestionMetadata question) => new()
    {
        Id = question.Id,
        SectionId = question.SectionId,
        ShortName = question.ShortName,
        QuestionNumber = question.QuestionNumber,
        IsPerSpecies = question.IsPerSpecies,
        IsRepeating = question.IsRepeating,
        Fields = [.. question.Fields.Select(ToDto)]
    };

    /// <summary>Projects a profile field.</summary>
    /// <param name="field">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileFieldMetadataDto ToDto(this ProfileFieldMetadata field) => new()
    {
        Id = field.Id,
        QuestionId = field.QuestionId,
        Name = field.Name,
        ShortName = field.ShortName,
        FieldNumber = field.FieldNumber,
        DataFieldTypeId = field.DataFieldTypeId,
        DataTypeName = field.DataTypeName,
        IsMandatory = field.IsMandatory,
        ReferenceTableId = field.ReferenceTableId,
        ReferenceTableIsMaintainable = field.ReferenceTableIsMaintainable
    };

    /// <summary>Projects one profile version section's recorded answers.</summary>
    /// <param name="answers">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileSectionAnswersDto ToDto(this ProfileSectionAnswers answers) => new()
    {
        ProfileVersionId = answers.ProfileVersionId,
        ProfileSectionId = answers.ProfileSectionId,
        QuestionNames = [.. answers.QuestionNames.Select(ToDto)],
        FieldValues = [.. answers.FieldValues.Select(ToDto)]
    };

    /// <summary>Projects a question's display names.</summary>
    /// <param name="questionName">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileQuestionNameDto ToDto(this ProfileQuestionName questionName) => new()
    {
        Id = questionName.Id,
        Name = questionName.Name,
        NonTechnicalName = questionName.NonTechnicalName
    };

    /// <summary>Projects a recorded field value.</summary>
    /// <param name="fieldValue">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileFieldValueDto ToDto(this ProfileFieldValue fieldValue) => new()
    {
        Id = fieldValue.Id,
        QuestionId = fieldValue.QuestionId,
        FieldNumber = fieldValue.FieldNumber,
        BooleanValue = fieldValue.BooleanValue,
        ListValue = fieldValue.ListValue,
        DecimalValue = fieldValue.DecimalValue,
        DateValue = fieldValue.DateValue,
        TextValue = fieldValue.TextValue
    };
}
