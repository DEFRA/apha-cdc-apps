using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
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
    private static readonly Guid RowId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTime FixedNow = new(2026, 10, 12, 9, 30, 0);

    private readonly Mock<IProfileSectionRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IProfileReportContentRepository> contentRepository = new(MockBehavior.Strict);
    private readonly Mock<IUserContext> userContext = new();

    public ProfileSectionServiceTests() =>
        userContext.SetupGet(context => context.UserId).Returns(AuditUserId);

    private ProfileSectionService CreateService() =>
        new(repository.Object, contentRepository.Object, userContext.Object, NullLogger<ProfileSectionService>.Instance, new FixedTimeProvider(FixedNow));

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
    public async Task GetProfileVersionValidationStatusAsync_MarksSectionValid_WhenTheSectionHasNoErrors()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: true);

        var result = await CreateService().GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileSectionTestData.ProfileVersionId);
        var section = result.Sections.Should().ContainSingle().Subject;
        section.ProfileSectionId.Should().Be(ProfileSectionTestData.SectionId);
        section.IsValid.Should().BeTrue();
        section.InvalidQuestionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_ListsTheInvalidQuestions_WhenAMandatoryFieldIsUnanswered()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false);

        var result = await CreateService().GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        var section = result.Sections.Should().ContainSingle().Subject;
        section.IsValid.Should().BeFalse();
        section.InvalidQuestionIds.Should().Equal(ProfileSectionTestData.QuestionId);
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_MarksSectionInvalidWithoutAnInvalidQuestion_WhenOnlyTheRevisionDatesAreIncomplete()
    {
        SetupValidation(revision: null, answerTheMandatoryField: true);

        var result = await CreateService().GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        var section = result.Sections.Should().ContainSingle().Subject;
        section.IsValid.Should().BeFalse();
        section.InvalidQuestionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsValid_WhenTheSectionHasNoErrors()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: true);

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.InvalidReasons.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsTheInvalidReasonsInTheLegacyFormat_WhenAMandatoryFieldIsMissing()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false);

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        var reason = result.InvalidReasons.Should().ContainSingle().Subject;
        reason.QuestionId.Should().Be(ProfileSectionTestData.QuestionId);
        reason.SectionNumber.Should().Be(3);
        reason.QuestionNumber.Should().Be(1);
        reason.QuestionShortName.Should().Be("Q1");
        reason.Description.Should().Be("\"Is it endemic? - Endemic\" is required");
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ListsTheRevisionDateErrorsBeforeTheQuestionErrors()
    {
        SetupValidation(revision: null, answerTheMandatoryField: false);

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.InvalidReasons.Should().HaveCount(10);
        result.InvalidReasons[0].Description.Should().Be("\"Revision dates, Policy review frequency\" is required");
        result.InvalidReasons[0].QuestionId.Should().Be(Guid.Empty);
        result.InvalidReasons[^1].QuestionId.Should().Be(ProfileSectionTestData.QuestionId);
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_RequiresEachReviewToBeComplete()
    {
        var revision = CompleteRevision() with { VaReviewStatusId = ProposedStatusId };
        SetupValidation(revision, answerTheMandatoryField: true);

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.InvalidReasons.Should().ContainSingle().Which.Description.Should().Be("\"Revision dates, VA review must be complete\"");
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsValid_WhenTheSectionDoesNotExist()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false);

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, Guid.NewGuid(), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.InvalidReasons.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ComparesAFutureDateWithTheCurrentLocalTime()
    {
        var section = QuestionnaireWithOneMandatoryField().Sections[0];
        var questionnaire = new ReportQuestionnaire
        {
            Sections =
            [
                section with
                {
                    Questions =
                    [
                        new ReportQuestion
                        {
                            Id = ProfileSectionTestData.QuestionId,
                            QuestionNumber = 3,
                            ShortName = "Q3",
                            Fields = [new ReportField { Id = ProfileSectionTestData.FieldId, Name = "Date", ShortName = "Date Of Most Recent Occurrence In GB", DataTypeName = "Date" }]
                        }
                    ]
                }
            ]
        };
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false, questionnaire: questionnaire, values:
        [
            new ReportFieldValue { QuestionId = ProfileSectionTestData.QuestionId, FieldId = ProfileSectionTestData.FieldId, DateValue = FixedNow.AddMinutes(1) }
        ]);

        var result = await CreateService().ValidateProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        result.InvalidReasons.Should().ContainSingle().Which.Description.Should().Contain("cannot be a future date");
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_AppliesAChangeThatClearsEveryAnswerRowAndTheRevisionData_AndReturnsTheClearedCount()
    {
        GivenClearInputs(isWhatIfScenario: false);
        ProfileSectionChange? applied = null;
        repository
            .Setup(repo => repo.ApplyProfileVersionSectionChangeAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<ProfileSectionChange>(), AuditUserId, It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, ProfileSectionChange, Guid, CancellationToken>((_, _, change, _, _) => applied = change)
            .ReturnsAsync(2);

        var result = await CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, RowVersion, CancellationToken.None);

        result.FieldsClearedCount.Should().Be(2);
        applied.Should().NotBeNull();
        applied.Clears.Should().ContainSingle().Which.ProfileFieldId.Should().Be(ProfileSectionTestData.FieldId);
        applied.QuestionRowIdsToDelete.Should().Equal(RowId);
        applied.ClearRevision.Should().BeTrue();
        applied.ExpectedLastUpdated.Should().Equal(RowVersion);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_LeavesAFieldThatIsReadOnlyForAScenario_WhenTheProfileIsAScenario()
    {
        GivenClearInputs(isWhatIfScenario: true);
        ProfileSectionChange? applied = null;
        repository
            .Setup(repo => repo.ApplyProfileVersionSectionChangeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProfileSectionChange>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, ProfileSectionChange, Guid, CancellationToken>((_, _, change, _, _) => applied = change)
            .ReturnsAsync(1);

        await CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, null, CancellationToken.None);

        applied.Should().NotBeNull();
        applied.Clears.Should().BeEmpty();
        applied.ExpectedLastUpdated.Should().BeNull();
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_ReturnsZero_WhenTheSectionDoesNotExist()
    {
        contentRepository
            .Setup(repo => repo.GetQuestionnaireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportQuestionnaire { Sections = [] });

        var result = await CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, null, CancellationToken.None);

        result.FieldsClearedCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_PropagatesAConcurrencyException()
    {
        GivenClearInputs(isWhatIfScenario: false);
        repository
            .Setup(repo => repo.ApplyProfileVersionSectionChangeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProfileSectionChange>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("The profile version section has been edited by another user"));

        var act = () => CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_AppliesTheAnswersThatChanged_AndReturnsTheSavedCount()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false);
        ProfileSectionChange? applied = null;
        repository
            .Setup(repo => repo.ApplyProfileVersionSectionChangeAsync(
                ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<ProfileSectionChange>(), AuditUserId, It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, ProfileSectionChange, Guid, CancellationToken>((_, _, change, _, _) => applied = change)
            .ReturnsAsync(1);

        var answers = new List<ProfileFieldAnswerInputDto>
        {
            new() { ProfileFieldId = ProfileSectionTestData.FieldId, BooleanValue = true }
        };

        var result = await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, RowVersion, CancellationToken.None);

        result.FieldsSavedCount.Should().Be(1);
        applied.Should().NotBeNull();
        var save = applied.Saves.Should().ContainSingle().Subject;
        save.ProfileFieldId.Should().Be(ProfileSectionTestData.FieldId);
        save.BooleanValue.Should().BeTrue();
        save.IsMultiValueList.Should().BeFalse();
        applied.ExpectedLastUpdated.Should().Equal(RowVersion);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_WritesNothing_WhenNoAnswerChanged()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: true);

        var answers = new List<ProfileFieldAnswerInputDto>
        {
            new() { ProfileFieldId = ProfileSectionTestData.FieldId, BooleanValue = true }
        };

        var result = await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, RowVersion, CancellationToken.None);

        result.FieldsSavedCount.Should().Be(0);
        repository.Verify(
            repo => repo.ApplyProfileVersionSectionChangeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProfileSectionChange>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_ReadsEverySectionsAnswers_BecauseAnAnswerInOneCanDecideTheRelevanceOfAQuestionInAnother()
    {
        var otherSectionId = Guid.NewGuid();
        var questionnaire = QuestionnaireWithOneMandatoryField();
        questionnaire = questionnaire with { Sections = [.. questionnaire.Sections, new ReportSection { Id = otherSectionId, SectionNumber = 4, Name = "Other" }] };
        SetupValidation(CompleteRevision(), answerTheMandatoryField: true, questionnaire: questionnaire);
        contentRepository
            .Setup(repo => repo.GetSectionAnswersAsync(ProfileSectionTestData.ProfileVersionId, otherSectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportSectionAnswers());

        await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, [], null, CancellationToken.None);

        contentRepository.Verify(
            repo => repo.GetSectionAnswersAsync(ProfileSectionTestData.ProfileVersionId, otherSectionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_PropagatesAConcurrencyException()
    {
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false);
        repository
            .Setup(repo => repo.ApplyProfileVersionSectionChangeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProfileSectionChange>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("The profile version section has been edited by another user"));

        var answers = new List<ProfileFieldAnswerInputDto> { new() { ProfileFieldId = ProfileSectionTestData.FieldId, BooleanValue = true } };

        var act = () => CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_ReturnsZero_WhenTheSectionDoesNotExist()
    {
        contentRepository
            .Setup(repo => repo.GetQuestionnaireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportQuestionnaire { Sections = [] });
        contentRepository
            .Setup(repo => repo.GetVersionContextAsync(ProfileSectionTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportVersionContext());

        var answers = new List<ProfileFieldAnswerInputDto> { new() { ProfileFieldId = ProfileSectionTestData.FieldId } };

        var result = await CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, answers, null, CancellationToken.None);

        result.FieldsSavedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_ListsTheQuestionsTheAnswersMakeIrrelevant()
    {
        var followUpQuestionId = Guid.NewGuid();
        var section = QuestionnaireWithOneMandatoryField().Sections[0];
        var questionnaire = new ReportQuestionnaire
        {
            Sections =
            [
                section with
                {
                    Questions =
                    [
                        .. section.Questions,
                        new ReportQuestion
                        {
                            Id = followUpQuestionId,
                            QuestionNumber = 2,
                            ShortName = "Q2",
                            IrrelevanceFieldId = ProfileSectionTestData.FieldId,
                            IrrelevanceFlag = false,
                            Fields = [new ReportField { Id = Guid.NewGuid(), Name = "Follow-up", ShortName = "Follow-up", DataTypeName = "Text" }]
                        }
                    ]
                }
            ]
        };
        SetupValidation(CompleteRevision(), answerTheMandatoryField: false, questionnaire: questionnaire);

        var result = await CreateService().GetProfileVersionValidationStatusAsync(ProfileSectionTestData.ProfileVersionId, CancellationToken.None);

        result.Sections.Should().ContainSingle().Which.IrrelevantQuestionIds.Should().Equal(followUpQuestionId);
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

    [Fact]
    public async Task ClearProfileVersionSectionAsync_Throws_WhenTheRequestHasNoKnownUser()
    {
        userContext.SetupGet(context => context.UserId).Returns((Guid?)null);

        var act = () => CreateService().ClearProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_Throws_WhenTheRequestHasNoKnownUser()
    {
        userContext.SetupGet(context => context.UserId).Returns((Guid?)null);

        var act = () => CreateService().SaveProfileVersionSectionAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, [], null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_Throws_WhenTheRequestHasNoKnownUser()
    {
        userContext.SetupGet(context => context.UserId).Returns((Guid?)null);

        var act = () => CreateService().SaveProfileVersionRevisionDatesAsync(
            ProfileSectionTestData.ProfileVersionId, [Input(ProfileSectionTestData.SectionId)], CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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

    private static ReportQuestionnaire QuestionnaireWithOneMandatoryField() => new()
    {
        Sections =
        [
            new ReportSection
            {
                Id = ProfileSectionTestData.SectionId,
                SectionNumber = 3,
                Name = "Epidemiology",
                ShortName = "Epi",
                Questions =
                [
                    new ReportQuestion
                    {
                        Id = ProfileSectionTestData.QuestionId,
                        QuestionNumber = 1,
                        ShortName = "Q1",
                        Fields =
                        [
                            new ReportField
                            {
                                Id = ProfileSectionTestData.FieldId,
                                FieldNumber = 1,
                                Name = "Endemic",
                                ShortName = "Endemic",
                                DataTypeName = "Boolean",
                                IsMandatory = true
                            }
                        ]
                    }
                ]
            }
        ]
    };

    private static ReportSectionRevision CompleteRevision() => new()
    {
        VaReviewFrequencyId = YearlyFrequencyId,
        PolicyReviewFrequencyId = YearlyFrequencyId,
        AuthorReviewFrequencyId = YearlyFrequencyId,
        VaReviewStatusId = CompleteStatusId,
        PolicyReviewStatusId = CompleteStatusId,
        AuthorReviewStatusId = CompleteStatusId
    };

    private void SetupValidation(
        ReportSectionRevision? revision,
        bool answerTheMandatoryField,
        ReportQuestionnaire? questionnaire = null,
        IReadOnlyList<ReportFieldValue>? values = null)
    {
        contentRepository
            .Setup(repo => repo.GetQuestionnaireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(questionnaire ?? QuestionnaireWithOneMandatoryField());
        contentRepository
            .Setup(repo => repo.GetSectionAnswersAsync(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportSectionAnswers
            {
                Revision = revision,
                QuestionNames = [new ProfileQuestionName { Id = ProfileSectionTestData.QuestionId, Name = "Is it endemic?", NonTechnicalName = "Is it endemic?" }],
                Values = values ?? (answerTheMandatoryField
                    ? [new ReportFieldValue { QuestionId = ProfileSectionTestData.QuestionId, FieldId = ProfileSectionTestData.FieldId, BooleanValue = true }]
                    : [])
            });
        contentRepository
            .Setup(repo => repo.GetVersionContextAsync(ProfileSectionTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportVersionContext());
        SetupLookups();
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

    /// <summary>A section whose one Boolean field, answered Yes, is read-only for a scenario, with one repeating row, for the clear tests.</summary>
    private void GivenClearInputs(bool isWhatIfScenario)
    {
        var section = QuestionnaireWithOneMandatoryField().Sections[0];
        var question = section.Questions[0];
        var questionnaire = new ReportQuestionnaire
        {
            Sections = [section with { Questions = [question with { Fields = [question.Fields[0] with { IsReadOnlyScenario = true }] }] }]
        };

        contentRepository
            .Setup(repo => repo.GetQuestionnaireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(questionnaire);
        contentRepository
            .Setup(repo => repo.GetSectionAnswersAsync(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportSectionAnswers
            {
                Rows = [new ReportQuestionRow(RowId, ProfileSectionTestData.QuestionId, 0)],
                Values = [new ReportFieldValue { QuestionId = ProfileSectionTestData.QuestionId, FieldId = ProfileSectionTestData.FieldId, BooleanValue = true }]
            });
        contentRepository
            .Setup(repo => repo.GetVersionContextAsync(ProfileSectionTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportVersionContext { IsWhatIfScenario = isWhatIfScenario });
    }
}
