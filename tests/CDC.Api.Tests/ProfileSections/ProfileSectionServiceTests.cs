using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
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

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_MarksSectionValid_WhenAllMandatoryFieldsAreAnswered()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataWithOneMandatoryField());
        repository
            .Setup(repo => repo.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileSectionAnswers
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId,
                QuestionNames = [],
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
            });

        var result = await CreateService().GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileSectionTestData.ProfileVersionId);
        result.Sections.Should().ContainSingle().Which.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_MarksSectionInvalid_WhenAMandatoryFieldIsUnanswered()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataWithOneMandatoryField());
        repository
            .Setup(repo => repo.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileSectionAnswers
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId,
                QuestionNames = [],
                FieldValues = []
            });

        var result = await CreateService().GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        result.Sections.Should().ContainSingle().Which.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsValid_WhenAllMandatoryFieldsAreAnswered()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataWithOneMandatoryField());
        repository
            .Setup(repo => repo.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileSectionAnswers
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId,
                QuestionNames = [],
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
            });

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.UnansweredQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsUnansweredQuestions_WhenAMandatoryFieldIsMissing()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataWithOneMandatoryField());
        repository
            .Setup(repo => repo.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileSectionAnswers
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId,
                QuestionNames = [],
                FieldValues = []
            });

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.UnansweredQuestions.Should().ContainSingle().Which.Should().Be("3.1 Q1");
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_ClearsEveryAnsweredField_AndReturnsTheClearedCount()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataWithOneMandatoryField());
        repository
            .Setup(repo => repo.GetProfileSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileSectionAnswers
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId,
                QuestionNames = [],
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
            });
        repository
            .Setup(repo => repo.ClearProfileVersionSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                It.Is<IReadOnlyList<ProfileFieldClearInstruction>>(instructions =>
                    instructions.Count == 1
                    && instructions[0].ProfileFieldId == ProfileSectionTestData.FieldId
                    && !instructions[0].IsMultiValueList),
                Guid.Empty,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.FieldsClearedCount.Should().Be(1);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_ReturnsZero_WhenTheSectionDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileQuestionnaireMetadata { Sections = [] });

        var result = await CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.FieldsClearedCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_SavesAnsweredFields_AndReturnsTheSavedCount()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataWithOneMandatoryField());
        repository
            .Setup(repo => repo.SaveProfileVersionSectionAnswersAsync(
                ProfileSectionTestData.ProfileVersionId,
                ProfileSectionTestData.SectionId,
                It.Is<IReadOnlyList<ProfileFieldSaveInstruction>>(instructions =>
                    instructions.Count == 1
                    && instructions[0].ProfileFieldId == ProfileSectionTestData.FieldId
                    && instructions[0].BooleanValue == true
                    && !instructions[0].IsMultiValueList),
                Guid.Empty,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var answers = new List<ProfileFieldAnswerInputDto>
        {
            new() { ProfileFieldId = ProfileSectionTestData.FieldId, BooleanValue = true }
        };

        var result = await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, CancellationToken.None);

        result.FieldsSavedCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_IgnoresAnswers_ForFieldsBelongingToAPerSpeciesOrRepeatingQuestion()
    {
        var perSpeciesFieldId = Guid.NewGuid();
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
                            IsPerSpecies = true,
                            IsRepeating = false,
                            Fields =
                            [
                                new ProfileFieldMetadata
                                {
                                    Id = perSpeciesFieldId,
                                    QuestionId = ProfileSectionTestData.QuestionId,
                                    Name = "Is it endemic?",
                                    ShortName = "F1",
                                    FieldNumber = 1,
                                    DataFieldTypeId = ProfileSectionTestData.DataFieldTypeId,
                                    DataTypeName = "Boolean",
                                    IsMandatory = false,
                                    ReferenceTableId = Guid.Empty,
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

        var answers = new List<ProfileFieldAnswerInputDto>
        {
            new() { ProfileFieldId = perSpeciesFieldId, BooleanValue = true }
        };

        var result = await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, CancellationToken.None);

        result.FieldsSavedCount.Should().Be(0);
        repository.Verify(
            repo => repo.SaveProfileVersionSectionAnswersAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ProfileFieldSaveInstruction>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_ReturnsZero_WhenTheSectionDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileQuestionnaireMetadata { Sections = [] });

        var answers = new List<ProfileFieldAnswerInputDto> { new() { ProfileFieldId = ProfileSectionTestData.FieldId } };

        var result = await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, CancellationToken.None);

        result.FieldsSavedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileVersionSectionContributionsAsync_MapsRepositoryContributions()
    {
        var userId = Guid.NewGuid();
        repository
            .Setup(repo => repo.GetProfileVersionSectionContributionsAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProfileSectionContribution
                {
                    UserId = userId,
                    UserName = "jdoe",
                    OrganisationName = "APHA",
                    FullName = "Jane Doe",
                    LastContributionDate = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);

        var result = await CreateService().GetProfileVersionSectionContributionsAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        var contribution = result.Should().ContainSingle().Subject;
        contribution.UserId.Should().Be(userId);
        contribution.UserName.Should().Be("jdoe");
        contribution.OrganisationName.Should().Be("APHA");
        contribution.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetProfileSectionReviewCommentsAsync_MapsRepositoryComments()
    {
        var commentId = Guid.NewGuid();
        repository
            .Setup(repo => repo.GetProfileSectionReviewCommentsAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProfileReviewComment
                {
                    Id = commentId,
                    CommentSubject = "A comment",
                    UserFullName = "Jane Doe",
                    Level = 1,
                    ChildCount = 2
                }
            ]);

        var result = await CreateService().GetProfileSectionReviewCommentsAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        var comment = result.Should().ContainSingle().Subject;
        comment.Id.Should().Be(commentId);
        comment.CommentSubject.Should().Be("A comment");
        comment.UserFullName.Should().Be("Jane Doe");
        comment.Level.Should().Be(1);
        comment.ChildCount.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileVersionAllRevisionDatesAsync_ReturnsEverySectionsRevisionDates_InSectionNumberOrder()
    {
        var secondSectionId = Guid.NewGuid();
        var metadata = new ProfileQuestionnaireMetadata
        {
            Sections =
            [
                new ProfileSectionMetadata { Id = secondSectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 2, Questions = [] },
                new ProfileSectionMetadata { Id = ProfileSectionTestData.SectionId, Name = "Summary", ShortName = "Summ", SectionNumber = 1, Questions = [] }
            ]
        };

        repository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        repository
            .Setup(repo => repo.GetProfileVersionSectionRevisionInfoAsync(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileSectionRevisionInfo { ProfileSectionId = ProfileSectionTestData.SectionId, TechnicalReviewFrequencyMonths = 12 });
        repository
            .Setup(repo => repo.GetProfileVersionSectionRevisionInfoAsync(ProfileSectionTestData.ProfileVersionId, secondSectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileSectionRevisionInfo?)null);

        var result = await CreateService().GetProfileVersionAllRevisionDatesAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileSectionTestData.ProfileVersionId);
        result.Sections.Should().HaveCount(2);
        result.Sections[0].SectionName.Should().Be("Summary");
        result.Sections[0].TechnicalReviewFrequencyMonths.Should().Be(12);
        result.Sections[1].SectionName.Should().Be("Epidemiology");
        result.Sections[1].TechnicalReviewFrequencyMonths.Should().BeNull();
    }

    private static ProfileQuestionnaireMetadata MetadataWithOneMandatoryField() => new()
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
                                Name = "Is it endemic?",
                                ShortName = "F1",
                                FieldNumber = 1,
                                DataFieldTypeId = ProfileSectionTestData.DataFieldTypeId,
                                DataTypeName = "Boolean",
                                IsMandatory = true,
                                ReferenceTableId = Guid.Empty,
                                ReferenceTableIsMaintainable = false
                            }
                        ]
                    }
                ]
            }
        ]
    };
}
