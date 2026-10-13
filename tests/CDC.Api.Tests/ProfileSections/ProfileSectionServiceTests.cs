using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Common.Contracts;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileSectionServiceTests
{
    private static readonly Guid YearlyFrequencyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SixMonthlyFrequencyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProposedStatusId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CompleteStatusId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly Guid AuditUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime FixedNow = new(2026, 10, 12, 9, 30, 0);

    private readonly Mock<IProfileSectionRepository> repository = new(MockBehavior.Strict);

    private ProfileSectionService CreateService() =>
        new(repository.Object, new SpeciesAuditOptions(AuditUserId), NullLogger<ProfileSectionService>.Instance, new FixedTimeProvider(FixedNow));

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
                AuditUserId,
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
                AuditUserId,
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
    public async Task GetProfileVersionAllRevisionDatesAsync_ReturnsEverySectionsRevisionData_InSectionNumberOrder_WithLookups()
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
            .ReturnsAsync(new ProfileSectionRevisionInfo
            {
                ProfileSectionId = ProfileSectionTestData.SectionId,
                VaReviewFrequencyId = YearlyFrequencyId,
                AuthorReviewStatusId = ProposedStatusId,
                NextPolicyReview = new DateTime(2027, 6, 1),
                LastUpdated = RowVersion
            });
        repository
            .Setup(repo => repo.GetProfileVersionSectionRevisionInfoAsync(ProfileSectionTestData.ProfileVersionId, secondSectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileSectionRevisionInfo?)null);
        SetupLookups();

        var result = await CreateService().GetProfileVersionAllRevisionDatesAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileSectionTestData.ProfileVersionId);
        result.Sections.Should().HaveCount(2);
        result.Sections[0].SectionName.Should().Be("Summary");
        result.Sections[0].VaReviewFrequencyId.Should().Be(YearlyFrequencyId);
        result.Sections[0].AuthorReviewStatusId.Should().Be(ProposedStatusId);
        result.Sections[0].NextPolicyReview.Should().Be(new DateTime(2027, 6, 1));
        result.Sections[0].LastUpdated.Should().Equal(RowVersion);
        result.Sections[1].SectionName.Should().Be("Epidemiology");
        result.Sections[1].VaReviewFrequencyId.Should().BeNull();
        result.Sections[1].LastUpdated.Should().BeEmpty();
        result.ReviewFrequencies.Select(item => item.Name).Should().Equal("Every year", "Every 6 months");
        result.ReviewStatuses.Select(item => item.Name).Should().Equal("Proposed", "Complete");
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_WritesOnlyChangedSections()
    {
        var changedSectionId = ProfileSectionTestData.SectionId;
        var unchangedSectionId = Guid.NewGuid();
        SetupLookups();
        SetupExistingRevision(changedSectionId, new ProfileSectionRevisionInfo { ProfileSectionId = changedSectionId, VaReviewFrequencyId = YearlyFrequencyId, LastUpdated = RowVersion });
        SetupExistingRevision(unchangedSectionId, new ProfileSectionRevisionInfo
        {
            ProfileSectionId = unchangedSectionId,
            NextAuthorReview = new DateTime(2027, 1, 1, 13, 45, 0),
            LastUpdated = RowVersion
        });

        IReadOnlyList<ProfileSectionRevisionUpdate>? written = null;
        repository
            .Setup(repo => repo.UpdateProfileVersionSectionRevisionsAsync(
                ProfileSectionTestData.ProfileVersionId, It.IsAny<IReadOnlyList<ProfileSectionRevisionUpdate>>(), AuditUserId, It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<ProfileSectionRevisionUpdate>, Guid, CancellationToken>((_, updates, _, _) => written = updates)
            .ReturnsAsync(1);

        var sections = new[]
        {
            Input(changedSectionId, vaFrequency: SixMonthlyFrequencyId),
            // Same calendar day as stored (the stored value has a time of day), so not a change.
            Input(unchangedSectionId, nextAuthorReview: new DateTime(2027, 1, 1))
        };

        var result = await CreateService().SaveProfileVersionRevisionDatesAsync(ProfileSectionTestData.ProfileVersionId, sections, CancellationToken.None);

        result.SectionsSavedCount.Should().Be(1);
        var update = written.Should().ContainSingle().Subject;
        update.ProfileSectionId.Should().Be(changedSectionId);
        update.VaReviewFrequencyId.Should().Be(SixMonthlyFrequencyId);
        update.LastUpdated.Should().Equal(RowVersion);
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_StampsCompletedDate_WhenStatusChangesToComplete()
    {
        SetupLookups();
        SetupExistingRevision(ProfileSectionTestData.SectionId, new ProfileSectionRevisionInfo
        {
            ProfileSectionId = ProfileSectionTestData.SectionId,
            AuthorReviewStatusId = ProposedStatusId,
            VaReviewStatusId = CompleteStatusId,
            PolicyReviewStatusId = CompleteStatusId,
            LastUpdated = RowVersion
        });
        var written = CaptureWrittenUpdates();

        // Author: Proposed -> Complete (stamped). VA: Complete -> Proposed (cleared).
        // Policy: Complete -> Complete with another field changed (not re-stamped).
        await CreateService().SaveProfileVersionRevisionDatesAsync(
            ProfileSectionTestData.ProfileVersionId,
            [Input(ProfileSectionTestData.SectionId, authorStatus: CompleteStatusId, vaStatus: ProposedStatusId, policyStatus: CompleteStatusId, policyFrequency: YearlyFrequencyId)],
            CancellationToken.None);

        var update = written().Should().ContainSingle().Subject;
        update.AuthorReviewCompleted.Should().Be(FixedNow);
        update.VaReviewCompleted.Should().BeNull();
        update.PolicyReviewCompleted.Should().BeNull();
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_PassesNullsThrough_WhenValuesAreCleared()
    {
        SetupLookups();
        SetupExistingRevision(ProfileSectionTestData.SectionId, new ProfileSectionRevisionInfo
        {
            ProfileSectionId = ProfileSectionTestData.SectionId,
            AuthorReviewFrequencyId = YearlyFrequencyId,
            NextAuthorReview = new DateTime(2027, 1, 1),
            AuthorReviewStatusId = ProposedStatusId,
            LastUpdated = RowVersion
        });
        var written = CaptureWrittenUpdates();

        await CreateService().SaveProfileVersionRevisionDatesAsync(
            ProfileSectionTestData.ProfileVersionId, [Input(ProfileSectionTestData.SectionId)], CancellationToken.None);

        var update = written().Should().ContainSingle().Subject;
        update.AuthorReviewFrequencyId.Should().BeNull();
        update.NextAuthorReview.Should().BeNull();
        update.AuthorReviewStatusId.Should().BeNull();
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_Throws_WhenAFrequencyOrStatusIsNotARecognisedOption()
    {
        SetupLookups();
        SetupExistingRevision(ProfileSectionTestData.SectionId, new ProfileSectionRevisionInfo { ProfileSectionId = ProfileSectionTestData.SectionId, LastUpdated = RowVersion });

        var act = () => CreateService().SaveProfileVersionRevisionDatesAsync(
            ProfileSectionTestData.ProfileVersionId,
            [Input(ProfileSectionTestData.SectionId, authorFrequency: Guid.NewGuid(), policyStatus: Guid.NewGuid())],
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            "Sections[0].AuthorReviewFrequencyId", "Sections[0].PolicyReviewStatusId");
        repository.Verify(
            repo => repo.UpdateProfileVersionSectionRevisionsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ProfileSectionRevisionUpdate>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_Throws_WhenASectionDoesNotExist()
    {
        SetupLookups();
        SetupExistingRevision(ProfileSectionTestData.SectionId, null);

        var act = () => CreateService().SaveProfileVersionRevisionDatesAsync(
            ProfileSectionTestData.ProfileVersionId, [Input(ProfileSectionTestData.SectionId)], CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Sections[0].ProfileSectionId");
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_WritesNothing_WhenNoSectionChanged()
    {
        SetupLookups();
        SetupExistingRevision(ProfileSectionTestData.SectionId, new ProfileSectionRevisionInfo { ProfileSectionId = ProfileSectionTestData.SectionId, LastUpdated = RowVersion });
        repository
            .Setup(repo => repo.UpdateProfileVersionSectionRevisionsAsync(
                ProfileSectionTestData.ProfileVersionId, It.Is<IReadOnlyList<ProfileSectionRevisionUpdate>>(updates => updates.Count == 0), AuditUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await CreateService().SaveProfileVersionRevisionDatesAsync(
            ProfileSectionTestData.ProfileVersionId, [Input(ProfileSectionTestData.SectionId)], CancellationToken.None);

        result.SectionsSavedCount.Should().Be(0);
    }

    private void SetupLookups()
    {
        repository
            .Setup(repo => repo.GetReviewFrequenciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProfileReviewLookupItem { Id = YearlyFrequencyId, Name = "Every year" },
                new ProfileReviewLookupItem { Id = SixMonthlyFrequencyId, Name = "Every 6 months" }
            ]);
        repository
            .Setup(repo => repo.GetReviewStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ProfileReviewLookupItem { Id = ProposedStatusId, Name = "Proposed" },
                new ProfileReviewLookupItem { Id = CompleteStatusId, Name = "Complete" }
            ]);
    }

    private void SetupExistingRevision(Guid sectionId, ProfileSectionRevisionInfo? info) =>
        repository
            .Setup(repo => repo.GetProfileVersionSectionRevisionInfoAsync(ProfileSectionTestData.ProfileVersionId, sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(info);

    private Func<IReadOnlyList<ProfileSectionRevisionUpdate>> CaptureWrittenUpdates()
    {
        IReadOnlyList<ProfileSectionRevisionUpdate> written = [];
        repository
            .Setup(repo => repo.UpdateProfileVersionSectionRevisionsAsync(
                ProfileSectionTestData.ProfileVersionId, It.IsAny<IReadOnlyList<ProfileSectionRevisionUpdate>>(), AuditUserId, It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<ProfileSectionRevisionUpdate>, Guid, CancellationToken>((_, updates, _, _) => written = updates)
            .ReturnsAsync(1);

        return () => written;
    }

    private static ProfileSectionRevisionDatesInputDto Input(
        Guid sectionId,
        Guid? authorFrequency = null,
        Guid? vaFrequency = null,
        Guid? policyFrequency = null,
        DateTime? nextAuthorReview = null,
        Guid? authorStatus = null,
        Guid? vaStatus = null,
        Guid? policyStatus = null) =>
        new()
        {
            ProfileSectionId = sectionId,
            AuthorReviewFrequencyId = authorFrequency,
            VaReviewFrequencyId = vaFrequency,
            PolicyReviewFrequencyId = policyFrequency,
            NextAuthorReview = nextAuthorReview,
            AuthorReviewStatusId = authorStatus,
            VaReviewStatusId = vaStatus,
            PolicyReviewStatusId = policyStatus,
            LastUpdated = RowVersion
        };

    private sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(localNow, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
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
