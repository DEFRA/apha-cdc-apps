using CDC.Api.Domain.Entities;
using CDC.Api.Features.Species.Dtos;

namespace CDC.Api.Features.Species.Mapping;

/// <summary>
/// Projects species domain entities onto the DTOs returned by the API.
/// </summary>
public static class SpeciesMappings
{
    /// <summary>Projects a species entity.</summary>
    /// <param name="species">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesDto ToDto(this Domain.Entities.Species species) => new()
    {
        Id = species.Id,
        ParentId = species.ParentId,
        Description = species.Description,
        IsActive = species.IsActive,
        IsInUse = species.IsInUse
    };

    /// <summary>Projects a selected species entity.</summary>
    /// <param name="species">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SelectedSpeciesDto ToDto(this SelectedSpecies species) => new()
    {
        Id = species.Id,
        ParentId = species.ParentId,
        Description = species.Description,
        IsActive = species.IsActive,
        IsInUse = species.IsInUse,
        DiseaseName = species.DiseaseName,
        Disease1 = species.Disease1,
        Disease2 = species.Disease2,
        Disease3 = species.Disease3,
        Disease4 = species.Disease4,
        Disease5 = species.Disease5,
        FilterNumber = species.FilterNumber
    };

    /// <summary>Projects the questionnaire metadata hierarchy.</summary>
    /// <param name="metadata">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesMetadataDto ToDto(this SpeciesMetadata metadata) => new()
    {
        Sections = [.. metadata.Sections.Select(ToDto)]
    };

    /// <summary>Projects a questionnaire section.</summary>
    /// <param name="section">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesSectionMetadataDto ToDto(this SpeciesSectionMetadata section) => new()
    {
        Id = section.Id,
        Name = section.Name,
        ShortName = section.ShortName,
        SectionNumber = section.SectionNumber,
        Questions = [.. section.Questions.Select(ToDto)]
    };

    /// <summary>Projects a question.</summary>
    /// <param name="question">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesQuestionMetadataDto ToDto(this SpeciesQuestionMetadata question) => new()
    {
        Id = question.Id,
        SectionId = question.SectionId,
        Name = question.Name,
        ShortName = question.ShortName,
        QuestionNumber = question.QuestionNumber,
        Fields = [.. question.Fields.Select(ToDto)]
    };

    /// <summary>Projects a field.</summary>
    /// <param name="field">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesFieldMetadataDto ToDto(this SpeciesFieldMetadata field) => new()
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
        ReferenceTableIsMaintainable = field.ReferenceTableIsMaintainable,
        EditorFieldType = field.EditorFieldType
    };

    /// <summary>Projects the answer data for a species.</summary>
    /// <param name="answerData">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesAnswerDataDto ToDto(this SpeciesAnswerData answerData) => new()
    {
        SpeciesId = answerData.SpeciesId,
        SpeciesName = answerData.SpeciesName,
        LastUpdated = answerData.LastUpdated,
        Sections = [.. answerData.Sections.Select(ToDto)]
    };

    /// <summary>Projects an answered section.</summary>
    /// <param name="section">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesSectionDto ToDto(this SpeciesSection section) => new()
    {
        SectionId = section.SectionId,
        FieldValues = [.. section.FieldValues.Select(ToDto)]
    };

    /// <summary>Projects a stored answer.</summary>
    /// <param name="fieldValue">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static SpeciesFieldValueDto ToDto(this SpeciesFieldValue fieldValue) => new()
    {
        Id = fieldValue.Id,
        QuestionId = fieldValue.QuestionId,
        FieldNumber = fieldValue.FieldNumber,
        BooleanValue = fieldValue.BooleanValue,
        ListValue = fieldValue.ListValue,
        TextValue = fieldValue.TextValue
    };
}
