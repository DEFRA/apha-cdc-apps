using CDC.Api.Domain.Entities;
using CDC.Api.Features.Species.Commands;

namespace CDC.Api.Tests.Species;

/// <summary>
/// Builders for species domain objects. The entities use <c>required</c> members, which
/// AutoFixture cannot populate, so they are constructed explicitly here.
/// </summary>
internal static class SpeciesTestData
{
    public static readonly Guid SpeciesId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid QuestionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid FieldId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ListValueId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static byte[] RowVersion => [0, 0, 0, 0, 0, 0, 7, 209];

    public static byte[] NewRowVersion => [0, 0, 0, 0, 0, 0, 7, 210];

    public static CDC.Api.Domain.Entities.Species Species(string description = "Cattle") => new()
    {
        Id = SpeciesId,
        ParentId = Guid.Empty,
        Description = description,
        IsActive = true,
        IsInUse = true
    };

    public static SelectedSpecies SelectedSpecies(string diseaseName = "Bovine tuberculosis") => new()
    {
        Id = SpeciesId,
        ParentId = Guid.Empty,
        Description = "Cattle",
        IsActive = true,
        IsInUse = true,
        DiseaseName = diseaseName,
        Disease1 = 0,
        Disease2 = 1,
        Disease3 = 2,
        Disease4 = 1,
        Disease5 = "Other",
        FilterNumber = 7
    };

    public static SpeciesMetadata Metadata() => new()
    {
        Sections =
        [
            new SpeciesSectionMetadata
            {
                Id = SectionId,
                Name = "Epidemiology",
                ShortName = "Epi",
                SectionNumber = 1,
                Questions =
                [
                    new SpeciesQuestionMetadata
                    {
                        Id = QuestionId,
                        SectionId = SectionId,
                        Name = "Is the disease endemic?",
                        ShortName = "Endemic",
                        QuestionNumber = 1,
                        Fields =
                        [
                            new SpeciesFieldMetadata
                            {
                                Id = FieldId,
                                QuestionId = QuestionId,
                                Name = "Endemic",
                                ShortName = "End",
                                FieldNumber = 1,
                                DataFieldTypeId = Guid.Empty,
                                DataTypeName = "Boolean",
                                IsMandatory = true,
                                ReferenceTableId = Guid.Empty,
                                ReferenceTableIsMaintainable = false,
                                EditorFieldType = 0
                            }
                        ]
                    }
                ]
            }
        ]
    };

    public static SpeciesAnswerData AnswerData() => new()
    {
        SpeciesId = SpeciesId,
        SpeciesName = "Cattle",
        LastUpdated = RowVersion,
        Sections =
        [
            new SpeciesSection
            {
                SectionId = SectionId,
                FieldValues =
                [
                    new SpeciesFieldValue
                    {
                        Id = FieldId,
                        QuestionId = QuestionId,
                        FieldNumber = 1,
                        BooleanValue = true
                    }
                ]
            }
        ]
    };

    public static UpdateSpeciesAnswerDataCommand UpdateCommand(params SpeciesFieldValueChange[] changes) => new()
    {
        SpeciesId = SpeciesId,
        LastUpdated = RowVersion,
        Changes = changes.Length > 0
            ? changes
            : [new SpeciesFieldValueChange { FieldId = FieldId, Kind = SpeciesFieldValueKind.Boolean, BooleanValue = true }]
    };
}
