using System.Data;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public sealed class ProfileSectionRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileSectionRepository>> logger = new();

    public ProfileSectionRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileSectionRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_AssemblesSectionsQuestionsAndFields()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileQuestionnaireMetadata, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "SectionNumber"],
                    [[ProfileSectionTestData.SectionId, "Epidemiology", "Epi", 3]]),
                new FakeResultSet(
                    ["ProfileSectionId", "Id", "ShortName", "QuestionNumber", "IsPerSpecies", "IsRepeating"],
                    [[ProfileSectionTestData.SectionId, ProfileSectionTestData.QuestionId, "Q1", 1, false, false]]),
                // Columns 0 and 1 duplicate ProfileSectionId/QuestionId; the repository reads
                // positionally rather than by name, exactly as the species metadata reader does.
                new FakeResultSet(
                    [
                        "ProfileSectionId", "QuestionId", "Id", "ShortName", "FieldNumber", "DataFieldTypeId",
                        "DataTypeName", "IsMandatory", "ReferenceTableId", "ReferenceTableIsMaintainable",
                        "AffectsRelevancy", "Name", "IsReadOnlyCurrentSituation", "IsReadOnlyScenario",
                        "IsPerSpecies", "IsRepeating", "IncludeInSummary"
                    ],
                    [
                        [
                            ProfileSectionTestData.SectionId, ProfileSectionTestData.QuestionId, ProfileSectionTestData.FieldId,
                            "F1", 1, ProfileSectionTestData.DataFieldTypeId, "List", true,
                            ProfileSectionTestData.ReferenceTableId, false, false, "Affected species",
                            false, false, false, false, false
                        ]
                    ])
            ]
        });

        var metadata = await CreateRepository().GetProfileQuestionnaireMetadataAsync(CancellationToken.None);

        var section = metadata.Sections.Should().ContainSingle().Subject;
        section.Id.Should().Be(ProfileSectionTestData.SectionId);
        section.Name.Should().Be("Epidemiology");
        section.ShortName.Should().Be("Epi");
        section.SectionNumber.Should().Be(3);

        var question = section.Questions.Should().ContainSingle().Subject;
        question.Id.Should().Be(ProfileSectionTestData.QuestionId);
        question.SectionId.Should().Be(ProfileSectionTestData.SectionId);
        question.ShortName.Should().Be("Q1");
        question.QuestionNumber.Should().Be(1);
        question.IsPerSpecies.Should().BeFalse();
        question.IsRepeating.Should().BeFalse();

        var field = question.Fields.Should().ContainSingle().Subject;
        field.Id.Should().Be(ProfileSectionTestData.FieldId);
        field.QuestionId.Should().Be(ProfileSectionTestData.QuestionId);
        field.Name.Should().Be("Affected species");
        field.ShortName.Should().Be("F1");
        field.FieldNumber.Should().Be(1);
        field.DataFieldTypeId.Should().Be(ProfileSectionTestData.DataFieldTypeId);
        field.DataTypeName.Should().Be("List");
        field.IsMandatory.Should().BeTrue();
        field.ReferenceTableId.Should().Be(ProfileSectionTestData.ReferenceTableId);
        field.ReferenceTableIsMaintainable.Should().BeFalse();
    }

    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_ReturnsEmptySections_WhenNoneExist()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileQuestionnaireMetadata, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "Name", "ShortName", "SectionNumber"),
                FakeResultSet.Empty("ProfileSectionId", "Id", "ShortName", "QuestionNumber", "IsPerSpecies", "IsRepeating"),
                FakeResultSet.Empty(
                    "ProfileSectionId", "QuestionId", "Id", "ShortName", "FieldNumber", "DataFieldTypeId",
                    "DataTypeName", "IsMandatory", "ReferenceTableId", "ReferenceTableIsMaintainable",
                    "AffectsRelevancy", "Name", "IsReadOnlyCurrentSituation", "IsReadOnlyScenario",
                    "IsPerSpecies", "IsRepeating", "IncludeInSummary")
            ]
        });

        var metadata = await CreateRepository().GetProfileQuestionnaireMetadataAsync(CancellationToken.None);

        metadata.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileQuestionnaireMetadata, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileQuestionnaireMetadataAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_MapsQuestionNamesAndFieldValues()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "Name", "IsActive"),
                FakeResultSet.Empty(
                    "ProfileSectionId", "TechnicalReviewFrequency", "PolicyReviewFrequency", "NextTechnicalReview",
                    "NextPolicyReview", "LastUpdated", "ParentId", "PolicyReviewStatusId", "TechnicalReviewStatusId"),
                new FakeResultSet(
                    ["Id", "Name", "NonTechnicalName"],
                    [[ProfileSectionTestData.QuestionId, "Is it endemic?", "Is the disease already present?"]]),
                FakeResultSet.Empty("Id", "ProfileQuestionId", "SequenceNumber"),
                new FakeResultSet(
                    [
                        "Id", "SpeciesId", "ProfileVersionQuestionRowId", "BooleanValue", "ListValue", "DecimalValue",
                        "DateValue", "TextValue", "ProfileFieldId", "ProfileFieldGroupId", "QuestionNumber",
                        "QuestionId", "FieldNumber"
                    ],
                    [
                        [
                            ProfileSectionTestData.FieldValueId, null, null, true, null, null, null, null,
                            ProfileSectionTestData.FieldId, null, 1, ProfileSectionTestData.QuestionId, 1
                        ]
                    ])
            ]
        });

        var answers = await CreateRepository().GetProfileSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            CancellationToken.None);

        answers.ProfileVersionId.Should().Be(ProfileSectionTestData.ProfileVersionId);
        answers.ProfileSectionId.Should().Be(ProfileSectionTestData.SectionId);

        var questionName = answers.QuestionNames.Should().ContainSingle().Subject;
        questionName.Id.Should().Be(ProfileSectionTestData.QuestionId);
        questionName.Name.Should().Be("Is it endemic?");
        questionName.NonTechnicalName.Should().Be("Is the disease already present?");

        var fieldValue = answers.FieldValues.Should().ContainSingle().Subject;
        fieldValue.Id.Should().Be(ProfileSectionTestData.FieldValueId);
        fieldValue.QuestionId.Should().Be(ProfileSectionTestData.QuestionId);
        fieldValue.FieldNumber.Should().Be(1);
        fieldValue.BooleanValue.Should().BeTrue();
        fieldValue.ListValue.Should().BeNull();
        fieldValue.DecimalValue.Should().BeNull();
        fieldValue.DateValue.Should().BeNull();
        fieldValue.TextValue.Should().BeNull();

        connection.Executed.Should().ContainSingle()
            .Which.Parameters.Should().ContainKey("ProfileVersionId")
            .WhoseValue.Should().Be(ProfileSectionTestData.ProfileVersionId);
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_ReturnsEmpty_WhenSectionHasNoAnswers()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "Name", "IsActive"),
                FakeResultSet.Empty(
                    "ProfileSectionId", "TechnicalReviewFrequency", "PolicyReviewFrequency", "NextTechnicalReview",
                    "NextPolicyReview", "LastUpdated", "ParentId", "PolicyReviewStatusId", "TechnicalReviewStatusId"),
                FakeResultSet.Empty("Id", "Name", "NonTechnicalName"),
                FakeResultSet.Empty("Id", "ProfileQuestionId", "SequenceNumber"),
                FakeResultSet.Empty(
                    "Id", "SpeciesId", "ProfileVersionQuestionRowId", "BooleanValue", "ListValue", "DecimalValue",
                    "DateValue", "TextValue", "ProfileFieldId", "ProfileFieldGroupId", "QuestionNumber",
                    "QuestionId", "FieldNumber")
            ]
        });

        var answers = await CreateRepository().GetProfileSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            CancellationToken.None);

        answers.QuestionNames.Should().BeEmpty();
        answers.FieldValues.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task ClearProfileVersionSectionAnswersAsync_ExecutesEachInstruction_ThenLogsTheContribution_WithinOneTransaction()
    {
        connection.Script(ProfileSectionStoredProcedures.ClearProfileVersionFieldValue, new FakeCommandScript());
        connection.Script(ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue, new FakeCommandScript());
        connection.Script(ProfileSectionStoredProcedures.LogProfileVersionSectionContribution, new FakeCommandScript());

        var instructions = new List<CDC.Api.Domain.Entities.ProfileFieldClearInstruction>
        {
            new()
            {
                ProfileFieldId = ProfileSectionTestData.FieldId,
                SpeciesId = null,
                ProfileVersionQuestionRowId = null,
                IsMultiValueList = false
            },
            new()
            {
                ProfileFieldId = ProfileSectionTestData.ReferenceTableId,
                SpeciesId = null,
                ProfileVersionQuestionRowId = null,
                IsMultiValueList = true
            }
        };

        var clearedCount = await CreateRepository().ClearProfileVersionSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            instructions,
            Guid.Empty,
            CancellationToken.None);

        clearedCount.Should().Be(2);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileSectionStoredProcedures.ClearProfileVersionFieldValue && c.HadTransaction);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue && c.HadTransaction);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileSectionStoredProcedures.LogProfileVersionSectionContribution && c.HadTransaction);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAnswersAsync_ReturnsZero_AndExecutesNothing_WhenThereAreNoInstructions()
    {
        var clearedCount = await CreateRepository().ClearProfileVersionSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            [],
            Guid.Empty,
            CancellationToken.None);

        clearedCount.Should().Be(0);
        connection.Executed.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearProfileVersionSectionAnswersAsync_RollsBackAndRethrows_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.ClearProfileVersionFieldValue, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var instructions = new List<CDC.Api.Domain.Entities.ProfileFieldClearInstruction>
        {
            new()
            {
                ProfileFieldId = ProfileSectionTestData.FieldId,
                SpeciesId = null,
                ProfileVersionQuestionRowId = null,
                IsMultiValueList = false
            }
        };

        var act = () => CreateRepository().ClearProfileVersionSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            instructions,
            Guid.Empty,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task SaveProfileVersionSectionAnswersAsync_UpsertsSingleValueFields_AndReplacesMultiValueListFields_WithinOneTransaction()
    {
        connection.Script(ProfileSectionStoredProcedures.UpsertProfileVersionFieldValue, new FakeCommandScript());
        connection.Script(ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue, new FakeCommandScript());
        connection.Script(ProfileSectionStoredProcedures.InsertProfileVersionFieldMultiValue, new FakeCommandScript());
        connection.Script(ProfileSectionStoredProcedures.LogProfileVersionSectionContribution, new FakeCommandScript());

        var instructions = new List<CDC.Api.Domain.Entities.ProfileFieldSaveInstruction>
        {
            new() { ProfileFieldId = ProfileSectionTestData.FieldId, BooleanValue = true, IsMultiValueList = false },
            new()
            {
                ProfileFieldId = ProfileSectionTestData.ReferenceTableId,
                MultiListValues = [Guid.NewGuid(), Guid.NewGuid()],
                IsMultiValueList = true
            }
        };

        var savedCount = await CreateRepository().SaveProfileVersionSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            instructions,
            Guid.Empty,
            CancellationToken.None);

        savedCount.Should().Be(2);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileSectionStoredProcedures.UpsertProfileVersionFieldValue && c.HadTransaction);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue && c.HadTransaction);
        connection.Executed.Where(c => c.CommandText == ProfileSectionStoredProcedures.InsertProfileVersionFieldMultiValue).Should().HaveCount(2);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileSectionStoredProcedures.LogProfileVersionSectionContribution && c.HadTransaction);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAnswersAsync_ReturnsZero_AndExecutesNothing_WhenThereAreNoInstructions()
    {
        var savedCount = await CreateRepository().SaveProfileVersionSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            [],
            Guid.Empty,
            CancellationToken.None);

        savedCount.Should().Be(0);
        connection.Executed.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveProfileVersionSectionAnswersAsync_RollsBackAndRethrows_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.UpsertProfileVersionFieldValue, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var instructions = new List<CDC.Api.Domain.Entities.ProfileFieldSaveInstruction>
        {
            new() { ProfileFieldId = ProfileSectionTestData.FieldId, TextValue = "Some notes", IsMultiValueList = false }
        };

        var act = () => CreateRepository().SaveProfileVersionSectionAnswersAsync(
            ProfileSectionTestData.ProfileVersionId,
            ProfileSectionTestData.SectionId,
            instructions,
            Guid.Empty,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileVersionSectionContributionsAsync_MapsContributions()
    {
        var userId = Guid.NewGuid();

        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSectionContributions, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["ProfileVersionId", "UserId", "ProfileSectionId", "UserName", "Organisation", "FullName", "LastContributionDate"],
                    [[ProfileSectionTestData.ProfileVersionId, userId, ProfileSectionTestData.SectionId, "jdoe", "APHA", "Jane Doe", new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)]])
            ]
        });

        var contributions = await CreateRepository().GetProfileVersionSectionContributionsAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        var contribution = contributions.Should().ContainSingle().Subject;
        contribution.UserId.Should().Be(userId);
        contribution.UserName.Should().Be("jdoe");
        contribution.OrganisationName.Should().Be("APHA");
        contribution.FullName.Should().Be("Jane Doe");
        contribution.LastContributionDate.Should().Be(new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetProfileVersionSectionContributionsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSectionContributions, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileVersionSectionContributionsAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileSectionReviewCommentsAsync_MapsCommentsInThreadOrder()
    {
        var commentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        connection.Script(ProfileSectionStoredProcedures.GetProfileSectionReviewComments, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    [
                        "Id", "CommentDate", "Subject", "CommentReference", "Comment", "UserName", "FullName",
                        "ProfileVersionId", "LastUpdated", "ParentId", "Level", "RowCode", "ChildCount"
                    ],
                    [
                        [
                            commentId, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc), "A reply", "Ref 1", "Comment text", "jdoe", "Jane Doe",
                            ProfileSectionTestData.ProfileVersionId, null, parentId, 2, "        1        1", 0
                        ]
                    ])
            ]
        });

        var comments = await CreateRepository().GetProfileSectionReviewCommentsAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        var comment = comments.Should().ContainSingle().Subject;
        comment.Id.Should().Be(commentId);
        comment.CommentSubject.Should().Be("A reply");
        comment.CommentReference.Should().Be("Ref 1");
        comment.CommentText.Should().Be("Comment text");
        comment.UserName.Should().Be("jdoe");
        comment.UserFullName.Should().Be("Jane Doe");
        comment.ParentId.Should().Be(parentId);
        comment.Level.Should().Be(2);
        comment.ChildCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileSectionReviewCommentsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileSectionReviewComments, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileSectionReviewCommentsAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileVersionSectionRevisionInfoAsync_MapsRevisionDates()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "Name", "IsActive"),
                new FakeResultSet(
                    [
                        "ProfileSectionId", "TechnicalReviewFrequency", "PolicyReviewFrequency", "NextTechnicalReview",
                        "NextPolicyReview", "LastUpdated", "ParentId", "PolicyReviewStatusId", "TechnicalReviewStatusId"
                    ],
                    [[ProfileSectionTestData.SectionId, 12, 6, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc), null, null, null, null]])
            ]
        });

        var revisionInfo = await CreateRepository().GetProfileVersionSectionRevisionInfoAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        revisionInfo.Should().NotBeNull();
        revisionInfo.ProfileSectionId.Should().Be(ProfileSectionTestData.SectionId);
        revisionInfo.TechnicalReviewFrequencyMonths.Should().Be(12);
        revisionInfo.NextTechnicalReview.Should().Be(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        revisionInfo.PolicyReviewFrequencyMonths.Should().Be(6);
        revisionInfo.NextPolicyReview.Should().Be(new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetProfileVersionSectionRevisionInfoAsync_MapsNullFrequenciesAndDates()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "Name", "IsActive"),
                new FakeResultSet(
                    [
                        "ProfileSectionId", "TechnicalReviewFrequency", "PolicyReviewFrequency", "NextTechnicalReview",
                        "NextPolicyReview", "LastUpdated", "ParentId", "PolicyReviewStatusId", "TechnicalReviewStatusId"
                    ],
                    [[ProfileSectionTestData.SectionId, null, null, null, null, null, null, null, null]])
            ]
        });

        var revisionInfo = await CreateRepository().GetProfileVersionSectionRevisionInfoAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        revisionInfo.Should().NotBeNull();
        revisionInfo.TechnicalReviewFrequencyMonths.Should().BeNull();
        revisionInfo.NextTechnicalReview.Should().BeNull();
        revisionInfo.PolicyReviewFrequencyMonths.Should().BeNull();
        revisionInfo.NextPolicyReview.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileVersionSectionRevisionInfoAsync_ReturnsNull_WhenSectionHasNoRevisionRow()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty("Id", "Name", "IsActive"),
                FakeResultSet.Empty(
                    "ProfileSectionId", "TechnicalReviewFrequency", "PolicyReviewFrequency", "NextTechnicalReview",
                    "NextPolicyReview", "LastUpdated", "ParentId", "PolicyReviewStatusId", "TechnicalReviewStatusId")
            ]
        });

        var revisionInfo = await CreateRepository().GetProfileVersionSectionRevisionInfoAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        revisionInfo.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileVersionSectionRevisionInfoAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileSectionStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileVersionSectionRevisionInfoAsync(
            ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
