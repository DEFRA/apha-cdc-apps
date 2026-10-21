using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSections;
using CDC.Common.Contracts;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileVersionValidatorTests
{
    private static readonly Guid SectionId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid QuestionId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid FieldId = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid SecondFieldId = Guid.Parse("a0000000-0000-0000-0000-000000000004");
    private static readonly Guid ControllingQuestionId = Guid.Parse("a0000000-0000-0000-0000-000000000005");
    private static readonly Guid ControllingFieldId = Guid.Parse("a0000000-0000-0000-0000-000000000006");
    private static readonly Guid GroupFieldId = Guid.Parse("a0000000-0000-0000-0000-000000000007");
    private static readonly Guid ReferenceTableId = Guid.Parse("a0000000-0000-0000-0000-000000000008");
    private static readonly Guid CattleId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid SheepId = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid RowOneId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid RowTwoId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid YesId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid NoId = Guid.Parse("d0000000-0000-0000-0000-000000000002");
    private static readonly Guid ActiveItemId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid InactiveItemId = Guid.Parse("e0000000-0000-0000-0000-000000000002");
    private static readonly Guid CompleteStatusId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    private static readonly Guid ProposedStatusId = Guid.Parse("f0000000-0000-0000-0000-000000000002");
    private static readonly Guid FrequencyId = Guid.Parse("f0000000-0000-0000-0000-000000000003");
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0);

    private readonly Mock<IProfileReportContentRepository> repository = new(MockBehavior.Strict);

    [Fact]
    public async Task AMandatoryFieldWithNoAnswer_IsReportedAsRequired_WithTheQuestionAndFieldNames()
    {
        var section = Section(Question(QuestionId, 2, Field(FieldId, "Endemic")));

        var reasons = await ValidateAsync(section, Answers(names: [Name(QuestionId, "Is it endemic?")]));

        var reason = reasons.Should().ContainSingle().Subject;
        reason.Description.Should().Be("\"Is it endemic? - Endemic\" is required");
        reason.QuestionId.Should().Be(QuestionId);
        reason.SectionNumber.Should().Be(3);
        reason.QuestionNumber.Should().Be(2);
        reason.QuestionShortName.Should().Be("Q2");
    }

    [Fact]
    public async Task AnAnsweredMandatoryField_HasNoErrors()
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "Endemic")));

        var reasons = await ValidateAsync(section, Answers(values: [Value(FieldId, boolean: false)]));

        reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task AnOptionalFieldWithNoAnswer_HasNoErrors()
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "Endemic", mandatory: false)));

        var reasons = await ValidateAsync(section, Answers());

        reasons.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Text", null, true)]
    [InlineData("Text", "", true)]
    [InlineData("Text", " ", false)]
    [InlineData("Long Text", "<p>Answer</p>", false)]
    public async Task ATextFieldIsEmptyOnlyWhenItHasNoCharacters(string dataType, string? text, bool expectedRequired)
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "Comment", dataType)));
        List<ReportFieldValue> values = [];

        if (text is not null)
        {
            values.Add(Value(FieldId, text: text));
        }

        var reasons = await ValidateAsync(section, Answers(values: values));

        reasons.Any().Should().Be(expectedRequired);
    }

    [Fact]
    public async Task ADateFieldIsEmptyWhenItIsTheMinimumOrMaximumDate()
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "First seen", "Date"), Field(SecondFieldId, "Last seen", "Date")));

        var reasons = await ValidateAsync(
            section,
            Answers(values: [Value(FieldId, date: DateTime.MinValue), Value(SecondFieldId, date: DateTime.MaxValue)]));

        reasons.Select(reason => reason.Description).Should().Equal(
            "\"First seen\" is required",
            "\"Last seen\" is required");
    }

    [Fact]
    public async Task ADecimalOrListFieldIsEmptyWhenUnsetOrTheEmptyGuid()
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "Count", "Decimal"), Field(SecondFieldId, "Choice", "List")));

        var reasons = await ValidateAsync(section, Answers(values: [Value(SecondFieldId, list: Guid.Empty)]));

        reasons.Should().HaveCount(2);
    }

    [Fact]
    public async Task AMultiValueListFieldIsAnsweredWhenAnyOptionIsSelected()
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "Areas", "MultiValueList")));

        var empty = await ValidateAsync(section, Answers(values: [Value(FieldId)]));
        var answered = await ValidateAsync(section, Answers(values: [Value(FieldId, list: YesId), Value(FieldId, list: NoId)]));

        empty.Should().ContainSingle();
        answered.Should().BeEmpty();
    }

    [Fact]
    public async Task AProfileVersionDataFieldIsNeverEmpty()
    {
        var section = Section(Question(QuestionId, 1, Field(FieldId, "Species", "Profile Version Data")));

        var reasons = await ValidateAsync(section, Answers());

        reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task AFieldIsHidden_UnlessTheControllingListFieldHasTheAnswerItNames()
    {
        var dependent = Field(SecondFieldId, "Detail") with { RelevanceFieldId = ControllingFieldId, RelevanceValueId = YesId };
        var section = Section(Question(QuestionId, 1, Field(ControllingFieldId, "Present", "List", mandatory: false), dependent));

        var hidden = await ValidateAsync(section, Answers(values: [Value(ControllingFieldId, list: NoId)]));
        var unanswered = await ValidateAsync(section, Answers());
        var visible = await ValidateAsync(section, Answers(values: [Value(ControllingFieldId, list: YesId)]));

        hidden.Should().BeEmpty();
        unanswered.Should().BeEmpty();
        visible.Should().ContainSingle().Which.Description.Should().Be("\"Detail\" is required");
    }

    [Fact]
    public async Task ASelectedInactiveReferenceValue_IsReported_AndTheListIsReadOnlyOncePerTable()
    {
        var first = Field(FieldId, "Lab", "List", mandatory: false) with { ReferenceTableId = ReferenceTableId, ReferenceTableIsMaintainable = true };
        var second = Field(SecondFieldId, "Other lab", "MultiValueList", mandatory: false) with { ReferenceTableId = ReferenceTableId, ReferenceTableIsMaintainable = true };
        SetupReferenceItems();
        var section = Section(Question(QuestionId, 1, first, second));

        var reasons = await ValidateAsync(
            section,
            Answers(values: [Value(FieldId, list: InactiveItemId), Value(SecondFieldId, list: ActiveItemId), Value(SecondFieldId, list: InactiveItemId)]));

        reasons.Select(reason => reason.Description).Should().Equal(
            "\"Lab\" uses inactive reference data entry/entries",
            "\"Other lab\" uses inactive reference data entry/entries");
        repository.Verify(repo => repo.GetReferenceItemsAsync(ReferenceTableId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnActiveReferenceValue_HasNoErrors()
    {
        SetupReferenceItems();
        var field = Field(FieldId, "Lab", "List", mandatory: false) with { ReferenceTableId = ReferenceTableId, ReferenceTableIsMaintainable = true };

        var reasons = await ValidateAsync(Section(Question(QuestionId, 1, field)), Answers(values: [Value(FieldId, list: ActiveItemId)]));

        reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task AListThatIsNotMaintainable_IsNotCheckedForInactiveValues()
    {
        var field = Field(FieldId, "Lab", "List", mandatory: false) with { ReferenceTableId = ReferenceTableId };

        var reasons = await ValidateAsync(Section(Question(QuestionId, 1, field)), Answers(values: [Value(FieldId, list: InactiveItemId)]));

        reasons.Should().BeEmpty();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnAnsweredFieldInAnIrrelevantQuestion_IsReportedAsNotRelevant()
    {
        var section = Section(
            Question(ControllingQuestionId, 1, Field(ControllingFieldId, "Present", "Boolean", mandatory: false)),
            Question(QuestionId, 2, Field(FieldId, "Detail", "Text", mandatory: false)) with { IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });

        var reasons = await ValidateAsync(
            section,
            Answers(values: [Value(ControllingFieldId, boolean: true, questionId: ControllingQuestionId), Value(FieldId, text: "Some text")]));

        reasons.Should().ContainSingle().Which.Description.Should().Be("\"Detail\" is not relevant due to a previous selection");
    }

    [Fact]
    public async Task AnUnansweredFieldInAnIrrelevantQuestion_HasNoErrors_EvenWhenMandatory()
    {
        var section = Section(
            Question(ControllingQuestionId, 1, Field(ControllingFieldId, "Present", "Boolean", mandatory: false)),
            Question(QuestionId, 2, Field(FieldId, "Detail")) with { IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });

        var reasons = await ValidateAsync(section, Answers(values: [Value(ControllingFieldId, boolean: true, questionId: ControllingQuestionId)]));

        reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task AQuestionWhoseControllingFieldIsUnanswered_IsIrrelevant()
    {
        var section = Section(
            Question(QuestionId, 2, Field(FieldId, "Detail", "Text", mandatory: false)) with { IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });

        var reasons = await ValidateAsync(section, Answers(values: [Value(FieldId, text: "Some text")]));

        reasons.Should().ContainSingle().Which.Description.Should().Contain("is not relevant due to a previous selection");
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task ARelevanceRuleOnAListAnswer_FollowsTheIrrelevanceFlag(bool irrelevanceFlag, bool selectedTheNamedValue, bool expectedIrrelevant)
    {
        var section = Section(
            Question(QuestionId, 2, Field(FieldId, "Detail", "Text", mandatory: false))
                with
            { IrrelevanceFieldId = ControllingFieldId, IrrelevanceValueId = YesId, IrrelevanceFlag = irrelevanceFlag });
        var selected = selectedTheNamedValue ? YesId : NoId;

        var reasons = await ValidateAsync(
            section,
            Answers(values: [Value(ControllingFieldId, list: selected, questionId: ControllingQuestionId), Value(FieldId, text: "Some text")]));

        reasons.Any().Should().Be(expectedIrrelevant);
    }

    [Fact]
    public async Task ARelevanceRuleCanBeControlledByAFieldInAnotherSection()
    {
        var section = Section(
            Question(QuestionId, 2, Field(FieldId, "Detail", "Text", mandatory: false)) with { IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });
        var answers = Answers(values: [Value(FieldId, text: "Some text")]);
        var otherSectionValue = Value(ControllingFieldId, boolean: true, questionId: ControllingQuestionId);

        var reasons = await CreateValidator([otherSectionValue, .. answers.Values]).ValidateSectionAsync(section, answers, CancellationToken.None);

        reasons.Should().ContainSingle().Which.Description.Should().Contain("is not relevant");
    }

    [Fact]
    public async Task AFieldThatAffectsRelevance_IsNotReportedAsNotRelevant()
    {
        var field = Field(FieldId, "Detail", "Text", mandatory: false) with { AffectsRelevance = true };
        var section = Section(Question(QuestionId, 2, field) with { IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });

        var reasons = await ValidateAsync(section, Answers(values: [Value(FieldId, text: "Some text")]));

        reasons.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, false)]
    public async Task AReadOnlyByDefaultField_IsNotReportedAsNotRelevant_ForTheKindOfProfileItBelongsTo(
        bool isWhatIfScenario, bool readOnlyCurrentSituation, bool readOnlyScenario, bool expectedReported)
    {
        var field = Field(FieldId, "Detail", "Text", mandatory: false) with
        {
            IsReadOnlyCurrentSituation = readOnlyCurrentSituation,
            IsReadOnlyScenario = readOnlyScenario
        };
        var section = Section(Question(QuestionId, 2, field) with { IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });
        var answers = Answers(values: [Value(FieldId, text: "Some text")]);

        var reasons = await CreateValidator(answers.Values, new ReportVersionContext { IsWhatIfScenario = isWhatIfScenario })
            .ValidateSectionAsync(section, answers, CancellationToken.None);

        reasons.Any().Should().Be(expectedReported);
    }

    [Fact]
    public async Task TheDateOfMostRecentOccurrenceInTheFuture_IsReported()
    {
        var field = Field(FieldId, "Date", "Date", mandatory: false) with { ShortName = "Date Of Most Recent Occurrence In GB" };
        var section = Section(Question(QuestionId, 3, field));

        var future = await ValidateAsync(section, Answers(values: [Value(FieldId, date: Now.AddDays(1))]));
        var past = await ValidateAsync(section, Answers(values: [Value(FieldId, date: Now.AddDays(-1))]));
        var unanswered = await ValidateAsync(section, Answers());

        future.Should().ContainSingle().Which.Description.Should().Be("\"Date\" cannot be a future date");
        past.Should().BeEmpty();
        unanswered.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Profiled Species", true, false, true)]
    [InlineData("Profiled Species", false, true, false)]
    [InlineData("Other Affected Species", false, true, true)]
    [InlineData("Other Affected Species", true, false, false)]
    public async Task TheSpeciesLists_AreReportedWhenTheyIncludeAnInactiveSpecies(
        string shortName, bool inactiveProfiled, bool inactiveOther, bool expectedReported)
    {
        var field = Field(FieldId, "Species", "Profile Version Data", mandatory: false) with { ShortName = shortName };
        var section = Section(Question(QuestionId, 3, field));
        var answers = Answers();

        var reasons = await CreateValidator(
                answers.Values,
                new ReportVersionContext { HasInactiveProfiledSpecies = inactiveProfiled, HasInactiveOtherAffectedSpecies = inactiveOther })
            .ValidateSectionAsync(section, answers, CancellationToken.None);

        reasons.Any().Should().Be(expectedReported);
        if (expectedReported)
        {
            reasons.Single().Description.Should().Be("\"Species\" contains inactive species");
        }
    }

    [Fact]
    public async Task APerSpeciesQuestion_ReportsTheSpeciesThatAreMissingAnAnswer()
    {
        var section = Section(Question(QuestionId, 4, Field(FieldId, "Prevalence")) with { IsPerSpecies = true });
        var answers = Answers(
            species: [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep")],
            values: [Value(FieldId, boolean: true, speciesId: CattleId)],
            names: [Name(QuestionId, "Prevalence per species")]);

        var reasons = await ValidateAsync(section, answers);

        reasons.Should().ContainSingle().Which.Description.Should().Be("Sheep, \"Prevalence per species - Prevalence\" is required");
    }

    [Fact]
    public async Task AnInactiveSpecies_FailsEveryFieldOfAPerSpeciesQuestion_EvenWhenAnswered()
    {
        var section = Section(Question(QuestionId, 4, Field(FieldId, "Prevalence"), Field(SecondFieldId, "Notes", "Text", mandatory: false)) with { IsPerSpecies = true });
        var answers = Answers(
            species: [new ReportSpecies(CattleId, "Cattle", IsActive: false)],
            values: [Value(FieldId, boolean: true, speciesId: CattleId)]);

        var reasons = await ValidateAsync(section, answers);

        reasons.Select(reason => reason.Description).Should().Equal(
            "Cattle, \"Prevalence\" species is inactive",
            "Cattle, \"Notes\" species is inactive");
    }

    [Fact]
    public async Task APerSpeciesQuestion_IsRelevantPerSpecies_FromThatSpeciesOwnControllingAnswer()
    {
        var section = Section(
            Question(ControllingQuestionId, 1, Field(ControllingFieldId, "Present", "Boolean", mandatory: false)) with { IsPerSpecies = true },
            Question(QuestionId, 2, Field(FieldId, "Detail", "Text", mandatory: false)) with { IsPerSpecies = true, IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = false });
        var answers = Answers(
            species: [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep")],
            values:
            [
                Value(ControllingFieldId, boolean: true, speciesId: CattleId, questionId: ControllingQuestionId),
                Value(ControllingFieldId, boolean: false, speciesId: SheepId, questionId: ControllingQuestionId),
                Value(FieldId, text: "Cattle text", speciesId: CattleId),
                Value(FieldId, text: "Sheep text", speciesId: SheepId)
            ]);

        var reasons = await ValidateAsync(section, answers);

        reasons.Should().ContainSingle().Which.Description.Should().StartWith("Sheep, ").And.EndWith("is not relevant due to a previous selection");
    }

    [Theory]
    [InlineData(5, 3, null, null, "must be less than or equal to Typical incubation period range (lower value)")]
    [InlineData(1, 5, 3, null, "must be less than or equal to Typical incubation period range (upper value)")]
    [InlineData(1, 2, 9, 4, "must be less than or equal to Maximum incubation period (in days)")]
    [InlineData(5, null, null, null, null)]
    [InlineData(1, 2, 3, 4, null)]
    public async Task TheIncubationPeriods_MustNotExceedTheNextPeriodInTheSequence(
        int? minimum, int? typicalLower, int? typicalUpper, int? maximum, string? expectedText)
    {
        var minimumId = Guid.NewGuid();
        var lowerId = Guid.NewGuid();
        var upperId = Guid.NewGuid();
        var maximumId = Guid.NewGuid();
        var section = Section(Question(
            QuestionId,
            6,
            Field(lowerId, "Lower", "Decimal", mandatory: false) with { ShortName = "Incubation Period Typical Lower" },
            Field(upperId, "Upper", "Decimal", mandatory: false) with { ShortName = "Incubation Period Typical Upper" },
            Field(minimumId, "Minimum", "Decimal", mandatory: false) with { ShortName = "Incubation Period Minimum" },
            Field(maximumId, "Maximum", "Decimal", mandatory: false) with { ShortName = "Incubation Period Maximum" }) with
        { IsPerSpecies = true });
        var values = new List<ReportFieldValue>();
        AddDecimal(values, minimumId, minimum);
        AddDecimal(values, lowerId, typicalLower);
        AddDecimal(values, upperId, typicalUpper);
        AddDecimal(values, maximumId, maximum);

        var reasons = await ValidateAsync(section, Answers(species: [new ReportSpecies(CattleId, "Cattle")], values: values));

        if (expectedText is null)
        {
            reasons.Should().BeEmpty();
        }
        else
        {
            reasons.Should().ContainSingle().Which.Description.Should().EndWith(expectedText);
        }
    }

    [Fact]
    public async Task ARepeatingQuestion_ReportsEachRowByItsOneBasedNumber_InRowOrder()
    {
        var section = Section(Question(QuestionId, 5, Field(FieldId, "Name", "Text")) with { IsRepeating = true });
        var answers = Answers(
            rows: [new ReportQuestionRow(RowTwoId, QuestionId, 1), new ReportQuestionRow(RowOneId, QuestionId, 0)],
            values: [Value(FieldId, text: "Named", rowId: RowOneId)],
            names: [Name(QuestionId, "Laboratory")]);

        var reasons = await ValidateAsync(section, answers);

        reasons.Should().ContainSingle().Which.Description.Should().Be("Row 2, \"Laboratory - Name\" is required");
    }

    [Fact]
    public async Task AFieldGroupInARepeatingRow_IsValidatedForEachProfiledSpecies()
    {
        var childId = Guid.NewGuid();
        var group = Field(GroupFieldId, "Results", ReportField.FieldGroupDataType, mandatory: false) with
        {
            Children = [Field(childId, "Score", "Decimal")]
        };
        var section = Section(Question(QuestionId, 5, group) with { IsRepeating = true });
        var answers = Answers(
            species: [new ReportSpecies(CattleId, "Cattle"), new ReportSpecies(SheepId, "Sheep", IsActive: false)],
            rows: [new ReportQuestionRow(RowOneId, QuestionId, 0)],
            values: [Value(childId, decimalValue: 2m, speciesId: CattleId, rowId: RowOneId, groupId: GroupFieldId)],
            names: [Name(QuestionId, "Laboratory")]);

        var reasons = await ValidateAsync(section, answers);

        reasons.Should().ContainSingle().Which.Description.Should().Be("Sheep Row 1, \"Laboratory - Score\" species is inactive");
    }

    [Fact]
    public async Task AFieldGroupSpecies_IsAlwaysRelevant_SoAMissingAnswerIsRequired()
    {
        var childId = Guid.NewGuid();
        var group = Field(GroupFieldId, "Results", ReportField.FieldGroupDataType, mandatory: false) with
        {
            Children = [Field(childId, "Score", "Decimal")]
        };
        var section = Section(Question(QuestionId, 5, group) with { IsRepeating = true, IrrelevanceFieldId = ControllingFieldId, IrrelevanceFlag = true });
        var answers = Answers(
            species: [new ReportSpecies(CattleId, "Cattle")],
            rows: [new ReportQuestionRow(RowOneId, QuestionId, 0)],
            names: [Name(QuestionId, "Laboratory")]);

        var reasons = await ValidateAsync(section, answers);

        reasons.Should().ContainSingle().Which.Description.Should().Be("Cattle Row 1, \"Laboratory - Score\" is required");
    }

    [Fact]
    public async Task TheRevisionDates_AreValidatedFirst_ThenEachQuestionInOrder()
    {
        var section = Section(
            Question(QuestionId, 1, Field(FieldId, "First")),
            Question(ControllingQuestionId, 2, Field(SecondFieldId, "Second")));

        var reasons = await ValidateAsync(section, Answers(withoutRevision: true));

        reasons.Select(reason => reason.Description).Should().Equal(
            "\"Revision dates, Policy review frequency\" is required",
            "\"Revision dates, VA review frequency\" is required",
            "\"Revision dates, Author review frequency\" is required",
            "\"Revision dates, Policy review status\" is required",
            "\"Revision dates, VA review status\" is required",
            "\"Revision dates, Author review status\" is required",
            "\"Revision dates, VA review must be complete\"",
            "\"Revision dates, Policy review must be complete\"",
            "\"Revision dates, Author review must be complete\"",
            "\"First\" is required",
            "\"Second\" is required");
        reasons.Take(9).Should().OnlyContain(reason => reason.QuestionId == Guid.Empty && reason.SectionNumber == 3 && reason.QuestionNumber == 0);
        reasons.Skip(9).Select(reason => reason.QuestionId).Should().Equal(QuestionId, ControllingQuestionId);
    }

    [Fact]
    public async Task EachReviewMustHaveTheCompleteStatus()
    {
        var revision = CompleteRevision() with { PolicyReviewStatusId = ProposedStatusId, AuthorReviewStatusId = Guid.Empty };

        var reasons = await ValidateAsync(Section(), Answers(revision: revision));

        reasons.Select(reason => reason.Description).Should().Equal(
            "\"Revision dates, Author review status\" is required",
            "\"Revision dates, Policy review must be complete\"",
            "\"Revision dates, Author review must be complete\"");
    }

    [Fact]
    public async Task ASectionWithCompleteRevisionsAndNoQuestions_IsValid()
    {
        var reasons = await ValidateAsync(Section(), Answers());

        reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task TheNextReviewDates_AreNotCheckedWhenASingleSectionIsValidated()
    {
        var reasons = await ValidateAsync(Section(), Answers(revision: CompleteRevision() with { NextPolicyReview = null }));

        reasons.Should().BeEmpty();
    }

    [Fact]
    public void ASectionWithNoRevisionRecord_IsMissingEveryNextReviewDate_InPolicyVaAuthorOrder()
    {
        var reasons = ProfileVersionValidator.NextReviewReasons(7, revision: null).ToList();

        reasons.Select(reason => reason.Description).Should().Equal(
            "\"Revision dates, Next policy review\" is required",
            "\"Revision dates, Next VA review\" is required",
            "\"Revision dates, Next author review\" is required");
        reasons.Should().OnlyContain(reason => reason.SectionNumber == 7 && reason.QuestionNumber == 0 && reason.QuestionId == Guid.Empty && reason.QuestionShortName == string.Empty);
    }

    [Fact]
    public void ASectionWithEveryNextReviewDateSet_HasNoNextReviewErrors()
    {
        var revision = CompleteRevision() with
        {
            NextPolicyReview = Now.AddMonths(6),
            NextVaReview = Now.AddMonths(6),
            NextAuthorReview = Now.AddMonths(6)
        };

        ProfileVersionValidator.NextReviewReasons(3, revision).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, false, false, "\"Revision dates, Next policy review\" is required")]
    [InlineData(false, true, false, "\"Revision dates, Next VA review\" is required")]
    [InlineData(false, false, true, "\"Revision dates, Next author review\" is required")]
    public void OnlyTheNextReviewDatesThatAreNotSet_AreReported(bool policyMissing, bool vaMissing, bool authorMissing, string expected)
    {
        var revision = new ReportSectionRevision
        {
            NextPolicyReview = policyMissing ? null : Now,
            NextVaReview = vaMissing ? null : Now,
            NextAuthorReview = authorMissing ? null : Now
        };

        ProfileVersionValidator.NextReviewReasons(3, revision).Should().ContainSingle().Which.Description.Should().Be(expected);
    }

    [Fact]
    public void ANextReviewDateIsNotSet_WhenItIsTheMinimumDate_ButAnyOtherDateIsSet()
    {
        var revision = new ReportSectionRevision
        {
            NextPolicyReview = DateTime.MinValue,
            NextVaReview = new DateTime(1900, 1, 1),
            NextAuthorReview = DateTime.MaxValue
        };

        ProfileVersionValidator.NextReviewReasons(3, revision).Should().ContainSingle()
            .Which.Description.Should().Be("\"Revision dates, Next policy review\" is required");
    }

    private static ReportField Field(Guid id, string name, string dataType = "Boolean", bool mandatory = true) => new()
    {
        Id = id,
        Name = name,
        ShortName = name,
        DataTypeName = dataType,
        IsMandatory = mandatory
    };

    private static ReportQuestion Question(Guid id, int number, params ReportField[] fields) => new()
    {
        Id = id,
        QuestionNumber = number,
        ShortName = $"Q{number}",
        Fields = fields
    };

    private static ReportSection Section(params ReportQuestion[] questions) => new() { Id = SectionId, SectionNumber = 3, Questions = questions };

    private static ProfileQuestionName Name(Guid questionId, string name) => new() { Id = questionId, Name = name, NonTechnicalName = name };

    private static ReportSectionRevision CompleteRevision() => new()
    {
        VaReviewFrequencyId = FrequencyId,
        PolicyReviewFrequencyId = FrequencyId,
        AuthorReviewFrequencyId = FrequencyId,
        VaReviewStatusId = CompleteStatusId,
        PolicyReviewStatusId = CompleteStatusId,
        AuthorReviewStatusId = CompleteStatusId
    };

    private static ReportSectionAnswers Answers(
        ReportSectionRevision? revision = null,
        bool withoutRevision = false,
        IReadOnlyList<ReportSpecies>? species = null,
        IReadOnlyList<ReportQuestionRow>? rows = null,
        IReadOnlyList<ReportFieldValue>? values = null,
        IReadOnlyList<ProfileQuestionName>? names = null) => new()
        {
            Revision = withoutRevision ? null : revision ?? CompleteRevision(),
            ProfiledSpecies = species ?? [],
            Rows = rows ?? [],
            Values = values ?? [],
            QuestionNames = names ?? []
        };

    private static ReportFieldValue Value(
        Guid fieldId,
        bool? boolean = null,
        Guid? list = null,
        decimal? decimalValue = null,
        DateTime? date = null,
        string? text = null,
        Guid? speciesId = null,
        Guid? rowId = null,
        Guid? groupId = null,
        Guid? questionId = null) => new()
        {
            QuestionId = questionId ?? QuestionId,
            FieldId = fieldId,
            FieldGroupId = groupId ?? Guid.Empty,
            SpeciesId = speciesId,
            RowId = rowId,
            BooleanValue = boolean,
            ListValue = list,
            DecimalValue = decimalValue,
            DateValue = date,
            TextValue = text
        };

    private static void AddDecimal(List<ReportFieldValue> values, Guid fieldId, int? number)
    {
        if (number is { } value)
        {
            values.Add(Value(fieldId, decimalValue: value, speciesId: CattleId));
        }
    }

    private ProfileVersionValidator CreateValidator(IReadOnlyList<ReportFieldValue> allValues, ReportVersionContext? version = null) =>
        new(repository.Object, allValues, version ?? new ReportVersionContext(), CompleteStatusId, Now);

    private Task<IReadOnlyList<ProfileSectionInvalidReasonDto>> ValidateAsync(ReportSection section, ReportSectionAnswers answers) =>
        CreateValidator(answers.Values).ValidateSectionAsync(section, answers, CancellationToken.None);

    private void SetupReferenceItems() =>
        repository
            .Setup(repo => repo.GetReferenceItemsAsync(ReferenceTableId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReportReferenceItem(ActiveItemId, "Active", true), new ReportReferenceItem(InactiveItemId, "Inactive", false)]);
}
