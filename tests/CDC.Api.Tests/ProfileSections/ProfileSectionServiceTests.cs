using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileSectionServiceTests
{
    private readonly Mock<IProfileSectionRepository> repository = new(MockBehavior.Strict);

    private ProfileSectionService CreateService() =>
        new(repository.Object, NullLogger<ProfileSectionService>.Instance);

    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_MapsSectionsQuestionsAndFields()
    {
        var metadata = new ProfileQuestionnaireMetadata
        {
            Sections =
            [
                new ProfileSectionMetadata
                {
                    Id = ProfileSectionTestData.SectionId,
                    Name = "Epidemiology",
                    ShortName = "Epi",
                    SectionNumber = 3,
                    Questions =
                    [
                        new ProfileQuestionMetadata
                        {
                            Id = ProfileSectionTestData.QuestionId,
                            SectionId = ProfileSectionTestData.SectionId,
                            ShortName = "Q1",
                            QuestionNumber = 1,
                            IsPerSpecies = false,
                            IsRepeating = false,
                            Fields =
                            [
                                new ProfileFieldMetadata
                                {
                                    Id = ProfileSectionTestData.FieldId,
                                    QuestionId = ProfileSectionTestData.QuestionId,
                                    Name = "Affected species",
                                    ShortName = "F1",
                                    FieldNumber = 1,
                                    DataFieldTypeId = ProfileSectionTestData.DataFieldTypeId,
                                    DataTypeName = "List",
                                    IsMandatory = true,
                                    ReferenceTableId = ProfileSectionTestData.ReferenceTableId,
                                    ReferenceTableIsMaintainable = false
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var result = await CreateService().GetProfileQuestionnaireMetadataAsync(CancellationToken.None);

        var section = result.Sections.Should().ContainSingle().Subject;
        section.Name.Should().Be("Epidemiology");
        var question = section.Questions.Should().ContainSingle().Subject;
        question.ShortName.Should().Be("Q1");
        var field = question.Fields.Should().ContainSingle().Subject;
        field.Name.Should().Be("Affected species");
        field.DataTypeName.Should().Be("List");
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_MapsQuestionNamesAndFieldValues()
    {
        var answers = new ProfileSectionAnswers
        {
            ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
            ProfileSectionId = ProfileSectionTestData.SectionId,
            QuestionNames =
            [
                new ProfileQuestionName
                {
                    Id = ProfileSectionTestData.QuestionId,
                    Name = "Is it endemic?",
                    NonTechnicalName = "Is the disease already present?"
                }
            ],
            FieldValues =
            [
                new ProfileFieldValue
                {
                    Id = ProfileSectionTestData.FieldValueId,
                    QuestionId = ProfileSectionTestData.QuestionId,
                    FieldNumber = 1,
                    BooleanValue = true
                }
            ]
        };

        repository
            .Setup(repo => repo.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(answers);

        var result = await CreateService().GetProfileSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileSectionTestData.ProfileVersionId);
        result.ProfileSectionId.Should().Be(ProfileSectionTestData.SectionId);
        result.QuestionNames.Should().ContainSingle().Which.Name.Should().Be("Is it endemic?");
        result.FieldValues.Should().ContainSingle().Which.BooleanValue.Should().BeTrue();
    }
}
