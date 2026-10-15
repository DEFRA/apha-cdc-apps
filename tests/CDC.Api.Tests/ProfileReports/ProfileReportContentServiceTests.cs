using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportContentServiceTests
{
    private static readonly Guid VersionId = ProfileReportTestData.ProfileVersionId;
    private static readonly Guid SectionOneId = Guid.NewGuid();
    private static readonly Guid SectionTwoId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid CattleId = Guid.NewGuid();
    private static readonly Guid SheepId = Guid.NewGuid();

    private readonly Mock<IProfileReportRepository> reportRepository = new();
    private readonly Mock<IProfileReportContentRepository> contentRepository = new();

    private ProfileReportContentService CreateService() =>
        new(reportRepository.Object, contentRepository.Object, NullLogger<ProfileReportContentService>.Instance);

    [Fact]
    public async Task GetProfileReportContentAsync_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        reportRepository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var result = await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileReportContentAsync_MapsTheHeaderAndSectionDetails()
    {
        GivenHeader(isPublished: true, effectiveDateTo: null, statusName: "Active");
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 3 };
        GivenQuestionnaire(Section(SectionOneId, 1, "Description", "Desc.", "Read this", question));
        GivenAnswers(SectionOneId, new ReportSectionAnswers { QuestionNames = [Name(question.Id, "Is it?", "Is it here?")] });

        var content = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value;

        content.ProfileVersionId.Should().Be(VersionId);
        content.FullTitle.Should().Be("Avian influenza (What if)");
        content.VersionLabel.Should().Be("2.1");
        content.IsPublished.Should().BeTrue();
        content.EffectiveDateFrom.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        content.EffectiveDateTo.Should().BeNull();
        content.ProfileStatusName.Should().Be("Active");
        content.ShowDraftWatermark.Should().BeFalse();
        var section = content.Sections.Should().ContainSingle().Subject;
        section.Name.Should().Be("Description");
        section.ShortName.Should().Be("Desc.");
        section.Note.Should().Be("Read this");
        section.SectionNumber.Should().Be(1);
        var built = section.Questions.Should().ContainSingle().Subject;
        built.QuestionNumber.Should().Be(3);
        built.Name.Should().Be("Is it?");
        built.NonTechnicalName.Should().Be("Is it here?");
        built.Kind.Should().Be(ProfileReportQuestionKind.Standard);
        built.IsRelevant.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileReportContentAsync_ReturnsTheEffectiveEndDate_OfASupersededDraft()
    {
        GivenHeader(isPublished: false, effectiveDateTo: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), statusName: "In progress");
        GivenQuestionnaire();

        var content = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value;

        content.IsPublished.Should().BeFalse();
        content.EffectiveDateFrom.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        content.EffectiveDateTo.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        content.ProfileStatusName.Should().Be("In progress");
    }

    [Theory]
    [InlineData(true, false, "Validation complete", false)]
    [InlineData(false, false, "Validation complete", false)]
    [InlineData(false, false, "In progress", true)]
    [InlineData(false, false, "", true)]
    [InlineData(false, true, "Validation complete", true)]
    public async Task GetProfileReportContentAsync_StampsADraftUnlessItIsCurrentAndValidationComplete(
        bool isPublished,
        bool isSuperseded,
        string statusName,
        bool expected)
    {
        GivenHeader(isPublished, isSuperseded ? new DateTime(2026, 10, 1) : null, statusName);
        GivenQuestionnaire();

        var content = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value;

        content.ShowDraftWatermark.Should().Be(expected);
    }

    [Fact]
    public async Task GetProfileReportContentAsync_ResolvesEveryFieldTypeOfAStandardQuestion()
    {
        var activeId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var unusedOldId = Guid.NewGuid();
        var booleanField = Field("Boolean", 1, "Is it endemic?");
        var maintainableList = Field("List", 2, "Status", TableId, maintainable: true);
        var multiList = Field("MultiValueList", 3, "Routes", TableId, maintainable: true);
        var textField = Field("Text", 4, "Notes");
        var longText = Field("Long Text", 5, "Details");
        var decimalField = Field("Decimal", 6, "Rate");
        var dateField = Field("Date", 7, "Seen on");
        var emptyText = Field("Text", 8, "Unanswered");
        var question = new ReportQuestion
        {
            Id = Guid.NewGuid(),
            QuestionNumber = 1,
            Fields = [emptyText, dateField, decimalField, longText, textField, multiList, maintainableList, booleanField]
        };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "Description", "Desc.", string.Empty, question));
        contentRepository
            .Setup(repo => repo.GetReferenceItemsAsync(TableId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ReportReferenceItem(activeId, "Endemic", true),
                new ReportReferenceItem(otherId, "Exotic", true),
                new ReportReferenceItem(oldId, "Legacy", false),
                new ReportReferenceItem(unusedOldId, "Retired", false)
            ]);
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                Values =
                [
                    Value(question.Id, booleanField.Id, boolean: false),
                    Value(question.Id, maintainableList.Id, list: oldId),
                    Value(question.Id, multiList.Id, list: activeId),
                    Value(question.Id, multiList.Id, list: otherId),
                    Value(question.Id, textField.Id, text: "<p>Plain</p>"),
                    Value(question.Id, longText.Id, text: "Long"),
                    Value(question.Id, decimalField.Id, number: 1.5m),
                    Value(question.Id, dateField.Id, date: new DateTime(2026, 3, 4))
                ]
            });

        var content = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value;

        var fields = content.Sections[0].Questions[0].Groups.Should().ContainSingle().Subject.Fields;
        fields.Select(field => field.Label).Should().Equal("Is it endemic?", "Status", "Routes", "Notes", "Details", "Rate", "Seen on", "Unanswered");
        fields[0].Options.Should().Equal(new ProfileReportOptionDto("Yes", false), new ProfileReportOptionDto("No", true));
        fields[1].Options.Should().Equal(
            new ProfileReportOptionDto("Endemic", false),
            new ProfileReportOptionDto("Exotic", false),
            new ProfileReportOptionDto("Legacy (inactive)", true));
        fields[2].Options.Should().Equal(new ProfileReportOptionDto("Endemic", true), new ProfileReportOptionDto("Exotic", true));
        fields[3].Should().Match<ProfileReportFieldDto>(field => field.Kind == ProfileReportFieldKind.Text && field.Text == "<p>Plain</p>");
        fields[4].Text.Should().Be("Long");
        fields[5].Text.Should().Be("1.50");
        fields[6].Text.Should().Be("04 March 2026");
        fields[7].Should().Match<ProfileReportFieldDto>(field => field.Kind == ProfileReportFieldKind.Text && field.Text == string.Empty);
        contentRepository.Verify(repo => repo.GetReferenceItemsAsync(TableId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProfileReportContentAsync_ShowsAnUnansweredBooleanWithNeitherOptionTicked()
    {
        var booleanField = Field("Boolean", 1, "Is it?");
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [booleanField] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, question));
        GivenAnswers(SectionOneId, new ReportSectionAnswers());

        var content = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value;

        content.Sections[0].Questions[0].Groups[0].Fields[0].Options.Should().OnlyContain(option => !option.Selected);
    }

    [Fact]
    public async Task GetProfileReportContentAsync_BuildsOneGroupPerRow_InSequenceOrder_ForARepeatingQuestion()
    {
        var textField = Field("Text", 1, "Name");
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IsRepeating = true, Fields = [textField] };
        var firstRow = new ReportQuestionRow(Guid.NewGuid(), question.Id, 0);
        var secondRow = new ReportQuestionRow(Guid.NewGuid(), question.Id, 1);
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, question));
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                QuestionNames = [Name(question.Id, "Disease", string.Empty)],
                Rows = [secondRow, firstRow],
                Values =
                [
                    Value(question.Id, textField.Id, text: "Second", row: secondRow.Id),
                    Value(question.Id, textField.Id, text: "First", row: firstRow.Id)
                ]
            });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0];

        built.Kind.Should().Be(ProfileReportQuestionKind.Repeating);
        built.Groups.Select(group => group.Heading).Should().Equal("Disease 1", "Disease 2");
        built.Groups.Select(group => group.Fields[0].Text).Should().Equal("First", "Second");
    }

    [Fact]
    public async Task GetProfileReportContentAsync_HasNoGroups_ForARepeatingQuestionWithNoRows()
    {
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IsRepeating = true, Fields = [Field("Text", 1, "Name")] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, question));
        GivenAnswers(SectionOneId, new ReportSectionAnswers());

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0];

        built.IsRelevant.Should().BeTrue();
        built.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileReportContentAsync_BuildsOneGroupPerProfiledSpecies_ForAPerSpeciesQuestion()
    {
        var textField = Field("Text", 1, "Detail");
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IsPerSpecies = true, Fields = [textField] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, question));
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                ProfiledSpecies = [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep")],
                Values =
                [
                    Value(question.Id, textField.Id, text: "Moo", species: CattleId),
                    Value(question.Id, textField.Id, text: "Baa", species: SheepId)
                ]
            });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0];

        built.Kind.Should().Be(ProfileReportQuestionKind.PerSpecies);
        built.Groups.Select(group => group.Heading).Should().Equal("Cattle", "Sheep");
        built.Groups.Select(group => group.Fields[0].Text).Should().Equal("Moo", "Baa");
        built.Groups.Should().OnlyContain(group => group.IsRelevant);
    }

    [Fact]
    public async Task GetProfileReportContentAsync_PrintsAFieldGroupPerSpecies_WithItsChildFields()
    {
        var child = Field("Text", 1, "Child");
        var group = Field("Field Group", 1, "Group", children: [child]);
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [group] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, question));
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                ProfiledSpecies = [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep")],
                Values =
                [
                    Value(question.Id, child.Id, text: "Cattle child", species: CattleId, fieldGroup: group.Id),
                    Value(question.Id, child.Id, text: "Sheep child", species: SheepId, fieldGroup: group.Id)
                ]
            });

        var field = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0].Groups[0].Fields[0];

        field.Kind.Should().Be(ProfileReportFieldKind.FieldGroup);
        field.Groups.Select(inner => inner.Heading).Should().Equal("Cattle", "Sheep");
        field.Groups.Select(inner => inner.Fields[0].Text).Should().Equal("Cattle child", "Sheep child");
    }

    [Fact]
    public async Task GetProfileReportContentAsync_KeepsFieldGroupValuesInTheirRepeatingRow()
    {
        var child = Field("Text", 1, "Child");
        var group = Field("Field Group", 1, "Group", children: [child]);
        var question = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IsRepeating = true, Fields = [group] };
        var row = new ReportQuestionRow(Guid.NewGuid(), question.Id, 0);
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, question));
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                ProfiledSpecies = [new ReportSpecies(CattleId, "Cattle")],
                Rows = [row],
                Values =
                [
                    Value(question.Id, child.Id, text: "In row", species: CattleId, row: row.Id, fieldGroup: group.Id),
                    Value(question.Id, child.Id, text: "Other row", species: CattleId, row: Guid.NewGuid(), fieldGroup: group.Id)
                ]
            });

        var field = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0].Groups[0].Fields[0];

        field.Groups.Should().ContainSingle().Which.Fields[0].Text.Should().Be("In row");
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task GetProfileReportContentAsync_AppliesABooleanIrrelevanceRule(bool irrelevanceFlag, bool answer, bool expectedRelevant)
    {
        var controlling = Field("Boolean", 1, "Controls");
        var controlled = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 2, IrrelevanceFieldId = controlling.Id, IrrelevanceFlag = irrelevanceFlag, Fields = [Field("Text", 1, "T")] };
        var controllingQuestion = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [controlling] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, controllingQuestion, controlled));
        GivenAnswers(SectionOneId, new ReportSectionAnswers { Values = [Value(controllingQuestion.Id, controlling.Id, boolean: answer)] });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[1];

        built.IsRelevant.Should().Be(expectedRelevant);
        (built.Groups.Count > 0).Should().Be(expectedRelevant);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task GetProfileReportContentAsync_AppliesAListIrrelevanceRule(bool irrelevanceFlag, bool answerMatches, bool expectedRelevant)
    {
        var markerId = Guid.NewGuid();
        var controlling = Field("List", 1, "Controls", TableId);
        var controlled = new ReportQuestion
        {
            Id = Guid.NewGuid(),
            QuestionNumber = 2,
            IrrelevanceFieldId = controlling.Id,
            IrrelevanceValueId = markerId,
            IrrelevanceFlag = irrelevanceFlag,
            Fields = [Field("Text", 1, "T")]
        };
        var controllingQuestion = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [controlling] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, controllingQuestion, controlled));
        contentRepository
            .Setup(repo => repo.GetReferenceItemsAsync(TableId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GivenAnswers(SectionOneId, new ReportSectionAnswers { Values = [Value(controllingQuestion.Id, controlling.Id, list: answerMatches ? markerId : Guid.NewGuid())] });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[1];

        built.IsRelevant.Should().Be(expectedRelevant);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetProfileReportContentAsync_TreatsAnUnansweredControllingFieldAsIrrelevant(bool isList)
    {
        var controlling = Field(isList ? "List" : "Boolean", 1, "Controls", isList ? TableId : null);
        var controlled = new ReportQuestion
        {
            Id = Guid.NewGuid(),
            QuestionNumber = 2,
            IrrelevanceFieldId = controlling.Id,
            IrrelevanceValueId = isList ? Guid.NewGuid() : Guid.Empty,
            Fields = [Field("Text", 1, "T")]
        };
        var controllingQuestion = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [controlling] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, controllingQuestion, controlled));
        contentRepository
            .Setup(repo => repo.GetReferenceItemsAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GivenAnswers(SectionOneId, new ReportSectionAnswers { Values = [Value(controllingQuestion.Id, controlling.Id)] });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[1];

        built.IsRelevant.Should().BeFalse();
        built.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileReportContentAsync_TreatsAMissingControllingAnswerAsIrrelevant()
    {
        var controlled = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IrrelevanceFieldId = Guid.NewGuid(), Fields = [Field("Text", 1, "T")] };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, controlled));
        GivenAnswers(SectionOneId, new ReportSectionAnswers());

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0];

        built.IsRelevant.Should().BeFalse();
    }

    [Fact]
    public async Task GetProfileReportContentAsync_LetsAFieldInAnotherSectionControlRelevance()
    {
        var controlling = Field("Boolean", 1, "Controls");
        var controllingQuestion = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [controlling] };
        var controlled = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IrrelevanceFieldId = controlling.Id, IrrelevanceFlag = false, Fields = [Field("Text", 1, "T")] };
        GivenHeader();
        GivenQuestionnaire(
            Section(SectionOneId, 1, "One", "One", string.Empty, controlled),
            Section(SectionTwoId, 2, "Two", "Two", string.Empty, controllingQuestion));
        GivenAnswers(SectionOneId, new ReportSectionAnswers());
        GivenAnswers(SectionTwoId, new ReportSectionAnswers { Values = [Value(controllingQuestion.Id, controlling.Id, boolean: true)] });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[0];

        built.IsRelevant.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileReportContentAsync_AppliesRelevancePerSpecies_WhenTheControllingFieldIsPerSpecies()
    {
        var controlling = Field("Boolean", 1, "Controls");
        var controllingQuestion = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, IsPerSpecies = true, Fields = [controlling] };
        var controlled = new ReportQuestion
        {
            Id = Guid.NewGuid(),
            QuestionNumber = 2,
            IsPerSpecies = true,
            IrrelevanceFieldId = controlling.Id,
            IrrelevanceFlag = false,
            Fields = [Field("Text", 1, "T")]
        };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, controllingQuestion, controlled));
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                ProfiledSpecies = [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep")],
                Values =
                [
                    Value(controllingQuestion.Id, controlling.Id, boolean: true, species: CattleId),
                    Value(controllingQuestion.Id, controlling.Id, boolean: false, species: SheepId)
                ]
            });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[1];

        built.Groups.Select(group => group.IsRelevant).Should().Equal(true, false);
        built.Groups[1].Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileReportContentAsync_UsesTheNonSpeciesAnswerForEverySpecies_WhenTheControllingFieldIsNotPerSpecies()
    {
        var controlling = Field("Boolean", 1, "Controls");
        var controllingQuestion = new ReportQuestion { Id = Guid.NewGuid(), QuestionNumber = 1, Fields = [controlling] };
        var controlled = new ReportQuestion
        {
            Id = Guid.NewGuid(),
            QuestionNumber = 2,
            IsPerSpecies = true,
            IrrelevanceFieldId = controlling.Id,
            IrrelevanceFlag = true,
            Fields = [Field("Text", 1, "T")]
        };
        GivenHeader();
        GivenQuestionnaire(Section(SectionOneId, 1, "S", "S", string.Empty, controllingQuestion, controlled));
        GivenAnswers(
            SectionOneId,
            new ReportSectionAnswers
            {
                ProfiledSpecies = [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep")],
                Values = [Value(controllingQuestion.Id, controlling.Id, boolean: true)]
            });

        var built = (await CreateService().GetProfileReportContentAsync(VersionId, CancellationToken.None)).Value.Sections[0].Questions[1];

        built.Groups.Should().OnlyContain(group => !group.IsRelevant);
    }

    [Fact]
    public async Task GetProfilePrioritisationScoresAsync_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        reportRepository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var result = await CreateService().GetProfilePrioritisationScoresAsync(VersionId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        contentRepository.Verify(repo => repo.GetPrioritisationScoresAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProfilePrioritisationScoresAsync_ReturnsTheScores()
    {
        var scores = new ProfilePrioritisationScoresDto
        {
            CategoryScores = [new ProfileCategoryScoreDto { CategoryName = "Welfare" }],
            CriterionScores = [new ProfileCriterionScoreDto { CriterionName = "Pain" }]
        };
        GivenHeader();
        contentRepository
            .Setup(repo => repo.GetPrioritisationScoresAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);

        var result = await CreateService().GetProfilePrioritisationScoresAsync(VersionId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(scores);
    }

    private void GivenHeader(bool isPublished = false, DateTime? effectiveDateTo = null, string statusName = "Validation complete") =>
        reportRepository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionHeader
            {
                Id = VersionId,
                ProfileId = ProfileReportTestData.ProfileId,
                Title = "Avian influenza",
                ScenarioTitle = "What if",
                VersionMajor = 2,
                VersionMinor = 1,
                IsPublished = isPublished,
                EffectiveDateFrom = new DateTime(2026, 1, 1),
                EffectiveDateTo = effectiveDateTo,
                ProfileStatusName = statusName
            });

    private void GivenQuestionnaire(params ReportSection[] sections) =>
        contentRepository
            .Setup(repo => repo.GetQuestionnaireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportQuestionnaire { Sections = sections });

    private void GivenAnswers(Guid sectionId, ReportSectionAnswers answers) =>
        contentRepository
            .Setup(repo => repo.GetSectionAnswersAsync(VersionId, sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answers);

    private static ReportSection Section(Guid id, int number, string name, string shortName, string note, params ReportQuestion[] questions) =>
        new() { Id = id, SectionNumber = number, Name = name, ShortName = shortName, Note = note, Questions = questions };

    private static ProfileQuestionName Name(Guid id, string name, string nonTechnicalName) =>
        new() { Id = id, Name = name, NonTechnicalName = nonTechnicalName };

    private static ReportField Field(string dataType, int number, string name, Guid? tableId = null, bool maintainable = false, ReportField[]? children = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FieldNumber = number,
            Name = name,
            DataTypeName = dataType,
            ReferenceTableId = tableId ?? Guid.Empty,
            ReferenceTableIsMaintainable = maintainable,
            Children = children ?? []
        };

    private static ReportFieldValue Value(
        Guid questionId,
        Guid fieldId,
        bool? boolean = null,
        Guid? list = null,
        decimal? number = null,
        DateTime? date = null,
        string? text = null,
        Guid? species = null,
        Guid? row = null,
        Guid? fieldGroup = null) =>
        new()
        {
            QuestionId = questionId,
            FieldId = fieldId,
            FieldGroupId = fieldGroup ?? Guid.Empty,
            SpeciesId = species,
            RowId = row,
            BooleanValue = boolean,
            ListValue = list,
            DecimalValue = number,
            DateValue = date,
            TextValue = text
        };
}
