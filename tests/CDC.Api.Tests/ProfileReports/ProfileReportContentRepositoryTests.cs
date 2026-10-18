using System.Data;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportContentRepositoryTests : IDisposable
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid FieldId = Guid.NewGuid();
    private static readonly Guid GroupFieldId = Guid.NewGuid();
    private static readonly Guid ChildFieldId = Guid.NewGuid();
    private static readonly Guid ReferenceTableId = Guid.NewGuid();

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileReportContentRepository>> logger = new();

    public ProfileReportContentRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileReportContentRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetQuestionnaireAsync_BuildsSectionsQuestionsFieldsAndFieldGroupChildren()
    {
        var irrelevanceFieldId = Guid.NewGuid();
        var irrelevanceValueId = Guid.NewGuid();
        var relevanceFieldId = Guid.NewGuid();
        var relevanceValueId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetQuestionnaireMetadata, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Columns(5), [[SectionId, "Epidemiology", "Epid.", 2, "A note"], [Guid.NewGuid(), "Description", "Desc.", 1, null]]),
                new FakeResultSet(
                    Columns(9),
                    [
                        [SectionId, QuestionId, "Q 2", 2, false, true, irrelevanceFieldId, irrelevanceValueId, true],
                        [Guid.NewGuid(), Guid.NewGuid(), "Q1", 1, true, false, null, null, false]
                    ]),
                new FakeResultSet(
                    Columns(22),
                    [
                        [SectionId, QuestionId, ChildFieldId, "S", 2, Guid.NewGuid(), "Multi Value List", true, null, false, true, "Second", true, false, false, true, false, null, relevanceFieldId, relevanceValueId, false, false],
                        [SectionId, QuestionId, GroupFieldId, "G", 1, Guid.NewGuid(), "Field Group", false, ReferenceTableId, true, false, "Group", false, false, false, true, false, null, null, null, false, false]
                    ]),
                new FakeResultSet(
                    Columns(22),
                    [[SectionId, QuestionId, ChildFieldId, "C", 1, Guid.NewGuid(), "Text", false, null, false, false, "Child", false, true, false, true, false, GroupFieldId, null, null, false, false]]),
                FakeResultSet.Empty("Id", "Number")
            ]
        });

        var questionnaire = await CreateRepository().GetQuestionnaireAsync(CancellationToken.None);

        questionnaire.Sections.Select(section => section.SectionNumber).Should().Equal(1, 2);
        var section = questionnaire.Sections[1];
        section.Name.Should().Be("Epidemiology");
        section.Note.Should().Be("A note");
        questionnaire.Sections[0].Note.Should().BeEmpty();
        section.Questions.Select(question => question.QuestionNumber).Should().Equal(2);
        var question = section.Questions[0];
        question.ShortName.Should().Be("Q 2");
        question.IsRepeating.Should().BeTrue();
        question.IsPerSpecies.Should().BeFalse();
        question.IrrelevanceFieldId.Should().Be(irrelevanceFieldId);
        question.IrrelevanceValueId.Should().Be(irrelevanceValueId);
        question.IrrelevanceFlag.Should().BeTrue();
        question.Fields.Select(field => field.FieldNumber).Should().Equal(1, 2);
        var group = question.Fields[0];
        group.DataTypeName.Should().Be("Field Group");
        group.ReferenceTableId.Should().Be(ReferenceTableId);
        group.ReferenceTableIsMaintainable.Should().BeTrue();
        group.Children.Should().ContainSingle().Which.Name.Should().Be("Child");
        group.Children[0].IsReadOnlyScenario.Should().BeTrue();
        question.Fields[1].Children.Should().BeEmpty();
        var multiValueField = question.Fields[1];
        multiValueField.ShortName.Should().Be("S");
        multiValueField.DataTypeName.Should().Be("MultiValueList");
        multiValueField.IsMandatory.Should().BeTrue();
        multiValueField.AffectsRelevance.Should().BeTrue();
        multiValueField.IsReadOnlyCurrentSituation.Should().BeTrue();
        multiValueField.IsReadOnlyScenario.Should().BeFalse();
        multiValueField.RelevanceFieldId.Should().Be(relevanceFieldId);
        multiValueField.RelevanceValueId.Should().Be(relevanceValueId);
    }

    [Fact]
    public async Task GetQuestionnaireAsync_ToleratesMissingLaterResultSets()
    {
        connection.Script(ProfileReportStoredProcedures.GetQuestionnaireMetadata, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(Columns(5), [[SectionId, "Epidemiology", "Epid.", 1, null]])]
        });

        var questionnaire = await CreateRepository().GetQuestionnaireAsync(CancellationToken.None);

        questionnaire.Sections.Should().ContainSingle().Which.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQuestionnaireAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetQuestionnaireMetadata, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetQuestionnaireAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetSectionAnswersAsync_ReadsSpeciesNamesRowsAndEveryValueColumn()
    {
        var versionId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        var inactiveSpeciesId = Guid.NewGuid();
        var rowId = Guid.NewGuid();
        var listValue = Guid.NewGuid();
        var vaFrequency = Guid.NewGuid();
        var policyFrequency = Guid.NewGuid();
        var authorFrequency = Guid.NewGuid();
        var policyStatus = Guid.NewGuid();
        var vaStatus = Guid.NewGuid();
        var authorStatus = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Columns(3), [[speciesId, "Cattle", true], [inactiveSpeciesId, "Sheep", false]]),
                new FakeResultSet(
                    Columns(12),
                    [[SectionId, vaFrequency, policyFrequency, authorFrequency, null, null, null, null, Guid.Empty, policyStatus, vaStatus, authorStatus]]),
                new FakeResultSet(Columns(3), [[QuestionId, "Is it present?", "Is it here?"]]),
                new FakeResultSet(Columns(3), [[rowId, QuestionId, (byte)2]]),
                new FakeResultSet(
                    Columns(13),
                    [
                        [Guid.NewGuid(), speciesId, rowId, true, listValue, 1.5m, new DateTime(2026, 3, 4), "text", FieldId, Guid.Empty, 1, QuestionId, 1],
                        [Guid.NewGuid(), null, null, null, null, null, null, null, ChildFieldId, GroupFieldId, 1, QuestionId, 2]
                    ])
            ]
        });

        var answers = await CreateRepository().GetSectionAnswersAsync(versionId, SectionId, CancellationToken.None);

        answers.ProfiledSpecies.Should().Equal(
            new CDC.Api.Domain.Entities.ReportSpecies(speciesId, "Cattle"),
            new CDC.Api.Domain.Entities.ReportSpecies(inactiveSpeciesId, "Sheep", false));
        var revision = answers.Revision;
        revision.Should().NotBeNull();
        revision!.VaReviewFrequencyId.Should().Be(vaFrequency);
        revision.PolicyReviewFrequencyId.Should().Be(policyFrequency);
        revision.AuthorReviewFrequencyId.Should().Be(authorFrequency);
        revision.PolicyReviewStatusId.Should().Be(policyStatus);
        revision.VaReviewStatusId.Should().Be(vaStatus);
        revision.AuthorReviewStatusId.Should().Be(authorStatus);
        answers.QuestionNames.Should().ContainSingle().Which.NonTechnicalName.Should().Be("Is it here?");
        answers.Rows.Should().ContainSingle().Which.Should().Be(new CDC.Api.Domain.Entities.ReportQuestionRow(rowId, QuestionId, 2));
        answers.Values.Should().HaveCount(2);
        var first = answers.Values[0];
        first.SpeciesId.Should().Be(speciesId);
        first.RowId.Should().Be(rowId);
        first.BooleanValue.Should().BeTrue();
        first.ListValue.Should().Be(listValue);
        first.DecimalValue.Should().Be(1.5m);
        first.DateValue.Should().Be(new DateTime(2026, 3, 4));
        first.TextValue.Should().Be("text");
        first.FieldId.Should().Be(FieldId);
        first.FieldGroupId.Should().Be(Guid.Empty);
        first.QuestionId.Should().Be(QuestionId);
        answers.Values[1].FieldGroupId.Should().Be(GroupFieldId);
        answers.Values[1].SpeciesId.Should().BeNull();

        var command = connection.Executed.Should().ContainSingle().Subject;
        command.Parameters["ProfileVersionId"].Should().Be(versionId);
        command.Parameters["ProfileSectionId"].Should().Be(SectionId);
    }

    [Fact]
    public async Task GetSectionAnswersAsync_ToleratesMissingLaterResultSets()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id", "Name", "IsActive")]
        });

        var answers = await CreateRepository().GetSectionAnswersAsync(Guid.NewGuid(), SectionId, CancellationToken.None);

        answers.ProfiledSpecies.Should().BeEmpty();
        answers.Revision.Should().BeNull();
        answers.QuestionNames.Should().BeEmpty();
        answers.Rows.Should().BeEmpty();
        answers.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSectionAnswersAsync_LeavesRevisionNull_WhenTheVersionHasNoRecordOfTheSection()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionSection, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id", "Name", "IsActive"), FakeResultSet.Empty("SectionId")]
        });

        var answers = await CreateRepository().GetSectionAnswersAsync(Guid.NewGuid(), SectionId, CancellationToken.None);

        answers.Revision.Should().BeNull();
    }

    [Fact]
    public async Task GetSectionAnswersAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionSection, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetSectionAnswersAsync(Guid.NewGuid(), SectionId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetVersionContextAsync_ReadsTheScenarioFlag_AndWhichKindsOfSpeciesAreInactive()
    {
        var versionId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Columns(14), [[versionId, Guid.NewGuid(), "Title", 1, 0, "Draft", new DateTime(2026, 1, 1), null, true, "Scenario", Guid.NewGuid(), false, null, null]]),
                new FakeResultSet(
                    Columns(4),
                    [
                        [Guid.NewGuid(), "Cattle", "Profiled", true],
                        [Guid.NewGuid(), "Sheep", "Profiled", false],
                        [Guid.NewGuid(), "Goats", "Other", true]
                    ])
            ]
        });

        var context = await CreateRepository().GetVersionContextAsync(versionId, CancellationToken.None);

        context.IsWhatIfScenario.Should().BeTrue();
        context.HasInactiveProfiledSpecies.Should().BeTrue();
        context.HasInactiveOtherAffectedSpecies.Should().BeFalse();
        connection.Executed.Should().ContainSingle().Subject.Parameters["Id"].Should().Be(versionId);
    }

    [Fact]
    public async Task GetVersionContextAsync_FlagsInactiveOtherAffectedSpecies_ForTheCurrentSituation()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Columns(14), [[Guid.NewGuid(), Guid.NewGuid(), "Title", 1, 0, "Draft", new DateTime(2026, 1, 1), null, true, "", null, false, null, null]]),
                new FakeResultSet(Columns(4), [[Guid.NewGuid(), "Goats", "Other", false]])
            ]
        });

        var context = await CreateRepository().GetVersionContextAsync(Guid.NewGuid(), CancellationToken.None);

        context.IsWhatIfScenario.Should().BeFalse();
        context.HasInactiveProfiledSpecies.Should().BeFalse();
        context.HasInactiveOtherAffectedSpecies.Should().BeTrue();
    }

    [Fact]
    public async Task GetVersionContextAsync_ReturnsTheDefaults_WhenTheVersionDoesNotExist()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id")]
        });

        var context = await CreateRepository().GetVersionContextAsync(Guid.NewGuid(), CancellationToken.None);

        context.Should().Be(new CDC.Api.Domain.Entities.ReportVersionContext());
    }

    [Fact]
    public async Task GetVersionContextAsync_ToleratesAMissingSpeciesResultSet()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(Columns(14), [[Guid.NewGuid(), Guid.NewGuid(), "Title", 1, 0, "Draft", new DateTime(2026, 1, 1), null, true, "", null, false, null, null]])]
        });

        var context = await CreateRepository().GetVersionContextAsync(Guid.NewGuid(), CancellationToken.None);

        context.HasInactiveProfiledSpecies.Should().BeFalse();
        context.HasInactiveOtherAffectedSpecies.Should().BeFalse();
    }

    [Fact]
    public async Task GetVersionContextAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetProfileVersionInfoById, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetVersionContextAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetReferenceItemsAsync_UsesTheMaintainableProcedure_AndReadsTheActiveFlag()
    {
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetReferenceValuesByTable, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    Columns(6),
                    [
                        [activeId, "Yes please", null, null, false, true],
                        [inactiveId, "Old value", null, new DateTime(2020, 1, 1), true, false]
                    ])
            ]
        });

        var items = await CreateRepository().GetReferenceItemsAsync(ReferenceTableId, true, CancellationToken.None);

        items.Should().Equal(new CDC.Api.Domain.Entities.ReportReferenceItem(activeId, "Yes please", true), new CDC.Api.Domain.Entities.ReportReferenceItem(inactiveId, "Old value", false));
        connection.Executed.Should().ContainSingle().Subject.Parameters["ReferenceTableId"].Should().Be(ReferenceTableId);
    }

    [Fact]
    public async Task GetReferenceItemsAsync_UsesTheLookupProcedure_AndTreatsEveryValueAsActive()
    {
        var valueId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetLookupValuesByTable, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "LookupValue"], [[valueId, "Cattle"]])]
        });

        var items = await CreateRepository().GetReferenceItemsAsync(ReferenceTableId, false, CancellationToken.None);

        items.Should().ContainSingle().Which.Should().Be(new CDC.Api.Domain.Entities.ReportReferenceItem(valueId, "Cattle", true));
    }

    [Fact]
    public async Task GetReferenceItemsAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileReportStoredProcedures.GetLookupValuesByTable, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetReferenceItemsAsync(ReferenceTableId, false, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetPrioritisationScoresAsync_ReadsTheThreeProcedures_ByProfileVersion()
    {
        var versionId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();
        var speciesId = Guid.NewGuid();
        connection.Script(ProfileReportStoredProcedures.GetPrioritisationCategoryScores, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(Columns(8), [[categoryId, "Welfare", speciesId, "Cattle", true, 12, 3, true]])]
        });
        connection.Script(ProfileReportStoredProcedures.GetPrioritisationCriterionScores, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(Columns(12), [[categoryId, "Welfare", criterionId, "W1", "Pain", speciesId, "Cattle", false, true, "High", 4, "Animal welfare"]])
            ]
        });
        connection.Script(ProfileReportStoredProcedures.GetPrioritisedSpecies, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["SpeciesId", "Name"], [[speciesId, "Cattle"]])]
        });

        var scores = await CreateRepository().GetPrioritisationScoresAsync(versionId, CancellationToken.None);

        var category = scores.CategoryScores.Should().ContainSingle().Subject;
        category.CategoryId.Should().Be(categoryId);
        category.CategoryName.Should().Be("Welfare");
        category.SpeciesId.Should().Be(speciesId);
        category.SpeciesName.Should().Be("Cattle");
        category.DataIncomplete.Should().BeTrue();
        category.Score.Should().Be(12);
        category.Rank.Should().Be(3);
        category.IsProfiledSpecies.Should().BeTrue();
        var criterion = scores.CriterionScores.Should().ContainSingle().Subject;
        criterion.CriterionId.Should().Be(criterionId);
        criterion.CriterionCode.Should().Be("W1");
        criterion.CriterionName.Should().Be("Pain");
        criterion.DataIncomplete.Should().BeFalse();
        criterion.IrrelevantCategory.Should().BeTrue();
        criterion.CriterionValue.Should().Be("High");
        criterion.Score.Should().Be(4);
        criterion.CategoryDisplayName.Should().Be("Animal welfare");
        scores.Species.Should().ContainSingle().Which.Name.Should().Be("Cattle");
        connection.Executed.Should().HaveCount(3);
        connection.Executed.Should().OnlyContain(command => (Guid)command.Parameters["ProfileVersionId"]! == versionId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetPrioritisationScoresAsync_RethrowsAndLogs_WhenAnyProcedureFails(int failingProcedure)
    {
        string[] procedures =
        [
            ProfileReportStoredProcedures.GetPrioritisationCategoryScores,
            ProfileReportStoredProcedures.GetPrioritisationCriterionScores,
            ProfileReportStoredProcedures.GetPrioritisedSpecies
        ];

        for (var index = 0; index < procedures.Length; index++)
        {
            connection.Script(
                procedures[index],
                index + 1 == failingProcedure
                    ? new FakeCommandScript { Throws = new FakeDbException("boom") }
                    : new FakeCommandScript { ResultSets = [FakeResultSet.Empty("C0")] });
        }

        var act = () => CreateRepository().GetPrioritisationScoresAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private static string[] Columns(int count) => [.. Enumerable.Range(0, count).Select(index => $"C{index}")];

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
