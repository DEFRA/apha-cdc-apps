using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSections;
using CDC.Common.Contracts;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileSections;

/// <summary>
/// The section is built so each rule of the legacy save and clear has a question of its own:
/// <list type="bullet">
/// <item>Q1 "Endemic" (standard, always relevant): a Boolean, a Date, a Decimal and a List field.</item>
/// <item>Q2 "Details": relevant only while Q1 is answered Yes; holds a Text, a MultiValueList and a Boolean ("Confirmed") field.</item>
/// <item>Q3 "Follow-up": relevant only while Q2's "Confirmed" is answered Yes, so clearing Q2 cascades to it.</item>
/// <item>Q4 "Presence" (per species, always relevant) and Q5 "Details by species" (per species, a Text and a MultiValueList field): relevant for a species while Q4 is Yes for it.</item>
/// <item>Q6 "Rows" (repeating): relevant only while Q1 is answered Yes.</item>
/// <item>Q7 "Locked": relevant only while Q1 is answered Yes; one field read-only for the current situation, one for a scenario, one editable.</item>
/// </list>
/// </summary>
public class ProfileSectionChangePlannerTests
{
    private static readonly Guid SectionId = ProfileSectionTestData.SectionId;
    private static readonly Guid SpeciesA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid SpeciesB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private static readonly Guid RowOne = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid RowTwo = Guid.Parse("c2000000-0000-0000-0000-000000000002");
    private static readonly Guid FirstOption = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondOption = Guid.Parse("d2000000-0000-0000-0000-000000000002");
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly Guid EndemicQuestion = Guid.Parse("00000001-0000-0000-0000-000000000000");
    private static readonly Guid DetailsQuestion = Guid.Parse("00000002-0000-0000-0000-000000000000");
    private static readonly Guid FollowUpQuestion = Guid.Parse("00000003-0000-0000-0000-000000000000");
    private static readonly Guid PresenceQuestion = Guid.Parse("00000004-0000-0000-0000-000000000000");
    private static readonly Guid SpeciesDetailsQuestion = Guid.Parse("00000005-0000-0000-0000-000000000000");
    private static readonly Guid RowsQuestion = Guid.Parse("00000006-0000-0000-0000-000000000000");
    private static readonly Guid LockedQuestion = Guid.Parse("00000007-0000-0000-0000-000000000000");

    private static readonly Guid Endemic = Guid.Parse("f0000001-0000-0000-0000-000000000000");
    private static readonly Guid Since = Guid.Parse("f0000002-0000-0000-0000-000000000000");
    private static readonly Guid Count = Guid.Parse("f0000003-0000-0000-0000-000000000000");
    private static readonly Guid Level = Guid.Parse("f0000004-0000-0000-0000-000000000000");
    private static readonly Guid Notes = Guid.Parse("f0000005-0000-0000-0000-000000000000");
    private static readonly Guid Areas = Guid.Parse("f0000006-0000-0000-0000-000000000000");
    private static readonly Guid Confirmed = Guid.Parse("f0000007-0000-0000-0000-000000000000");
    private static readonly Guid FollowUp = Guid.Parse("f0000008-0000-0000-0000-000000000000");
    private static readonly Guid Present = Guid.Parse("f0000009-0000-0000-0000-000000000000");
    private static readonly Guid SpeciesNote = Guid.Parse("f000000a-0000-0000-0000-000000000000");
    private static readonly Guid RowText = Guid.Parse("f000000b-0000-0000-0000-000000000000");
    private static readonly Guid Locked = Guid.Parse("f000000c-0000-0000-0000-000000000000");
    private static readonly Guid ScenarioLocked = Guid.Parse("f000000d-0000-0000-0000-000000000000");
    private static readonly Guid Open = Guid.Parse("f000000e-0000-0000-0000-000000000000");
    private static readonly Guid SpeciesAreas = Guid.Parse("f000000f-0000-0000-0000-000000000000");
    private static readonly Guid RowAreas = Guid.Parse("f0000010-0000-0000-0000-000000000000");
    private static readonly Guid NewRow = Guid.Parse("c3000000-0000-0000-0000-000000000003");
    private static readonly Guid OtherQuestionRow = Guid.Parse("c4000000-0000-0000-0000-000000000004");

    private static ReportSection Section() => new()
    {
        Id = SectionId,
        SectionNumber = 3,
        Name = "Epidemiology",
        ShortName = "Epi",
        Questions =
        [
            Question(EndemicQuestion, 1, [Field(Endemic, "Boolean"), Field(Since, "Date"), Field(Count, "Decimal"), Field(Level, "List")]),
            Question(DetailsQuestion, 2, [Field(Notes, "Text"), Field(Areas, "MultiValueList"), Field(Confirmed, "Boolean")], controlledBy: Endemic),
            Question(FollowUpQuestion, 3, [Field(FollowUp, "Text")], controlledBy: Confirmed),
            Question(PresenceQuestion, 4, [Field(Present, "Boolean")], isPerSpecies: true),
            Question(SpeciesDetailsQuestion, 5, [Field(SpeciesNote, "Text"), Field(SpeciesAreas, "MultiValueList")], isPerSpecies: true, controlledBy: Present),
            Question(RowsQuestion, 6, [Field(RowText, "Text")], isRepeating: true, controlledBy: Endemic),
            Question(
                LockedQuestion,
                7,
                [Field(Locked, "Text", readOnlyCurrent: true), Field(ScenarioLocked, "Text", readOnlyScenario: true), Field(Open, "Text")],
                controlledBy: Endemic)
        ]
    };

    private static ReportQuestion Question(
        Guid id,
        int number,
        IReadOnlyList<ReportField> fields,
        bool isPerSpecies = false,
        bool isRepeating = false,
        Guid controlledBy = default) => new()
        {
            Id = id,
            QuestionNumber = number,
            ShortName = $"Q{number}",
            IsPerSpecies = isPerSpecies,
            IsRepeating = isRepeating,
            Fields = fields,

            // Relevant only while the controlling Boolean is answered Yes.
            IrrelevanceFieldId = controlledBy,
            IrrelevanceFlag = false
        };

    private static ReportField Field(Guid id, string dataType, bool readOnlyCurrent = false, bool readOnlyScenario = false) => new()
    {
        Id = id,
        Name = dataType,
        DataTypeName = dataType,
        IsReadOnlyCurrentSituation = readOnlyCurrent,
        IsReadOnlyScenario = readOnlyScenario
    };

    private static ReportFieldValue Boolean(Guid questionId, Guid fieldId, bool value, Guid? speciesId = null) =>
        new() { QuestionId = questionId, FieldId = fieldId, BooleanValue = value, SpeciesId = speciesId };

    private static ReportFieldValue Text(Guid questionId, Guid fieldId, string value, Guid? speciesId = null, Guid? rowId = null) =>
        new() { QuestionId = questionId, FieldId = fieldId, TextValue = value, SpeciesId = speciesId, RowId = rowId };

    private static ReportFieldValue Option(Guid questionId, Guid fieldId, Guid value) =>
        new() { QuestionId = questionId, FieldId = fieldId, ListValue = value };

    private static ReportSectionAnswers Answers(params ReportFieldValue[] values) => new()
    {
        ProfiledSpecies = [new ReportSpecies(SpeciesA, "Cattle"), new ReportSpecies(SpeciesB, "Sheep")],
        Rows = [new ReportQuestionRow(RowOne, RowsQuestion, 0), new ReportQuestionRow(RowTwo, RowsQuestion, 1)],
        Values = values
    };

    private static ProfileSectionChange PlanSave(
        ReportSectionAnswers answers,
        IReadOnlyList<ProfileFieldAnswerInputDto> posted,
        bool isWhatIfScenario = false,
        byte[]? lastUpdated = null,
        IReadOnlyList<ProfileRepeatingQuestionInputDto>? postedRows = null,
        ReportSection? section = null) =>
        ProfileSectionChangePlanner.PlanSave(
            section ?? Section(), new Dictionary<Guid, ReportSectionAnswers> { [SectionId] = answers }, isWhatIfScenario, posted, postedRows ?? [], lastUpdated);

    private static ProfileFieldAnswerInputDto Answer(Guid fieldId, bool? boolean = null, string? text = null, IReadOnlyList<Guid>? options = null, Guid? speciesId = null) =>
        new() { ProfileFieldId = fieldId, SpeciesId = speciesId, BooleanValue = boolean, TextValue = text, MultiListValues = options ?? [] };

    private static ProfileFieldClearInstruction Clear(Guid fieldId, bool isMultiValueList = false, Guid? speciesId = null) =>
        new() { ProfileFieldId = fieldId, SpeciesId = speciesId, IsMultiValueList = isMultiValueList };

    [Fact]
    public void PlanSave_HasNoValueChanges_WhenThePostedAnswersEqualWhatIsRecorded()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Text(DetailsQuestion, Notes, "Some notes"),
            Option(DetailsQuestion, Areas, FirstOption),
            Option(DetailsQuestion, Areas, SecondOption),
            Boolean(DetailsQuestion, Confirmed, true));

        var change = PlanSave(
            answers,
            [
                Answer(Endemic, boolean: true),
                Answer(Notes, text: "Some notes"),
                Answer(Areas, options: [SecondOption, FirstOption]),
                Answer(Confirmed, boolean: true)
            ]);

        change.ValueChangeCount.Should().Be(0);
        change.Saves.Should().BeEmpty();
        change.Clears.Should().BeEmpty();
        change.QuestionRowIdsToDelete.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_SavesOnlyTheAnswersThatChanged()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Text(DetailsQuestion, Notes, "Some notes"),
            Option(DetailsQuestion, Areas, FirstOption),
            Option(DetailsQuestion, Areas, SecondOption),
            Boolean(DetailsQuestion, Confirmed, true));

        var change = PlanSave(
            answers,
            [
                Answer(Endemic, boolean: true),
                Answer(Notes, text: "Other notes"),
                Answer(Areas, options: [FirstOption]),
                Answer(Confirmed, boolean: true)
            ]);

        change.Saves.Select(save => save.ProfileFieldId).Should().BeEquivalentTo([Notes, Areas]);
        change.Saves.Single(save => save.ProfileFieldId == Notes).TextValue.Should().Be("Other notes");
        var areas = change.Saves.Single(save => save.ProfileFieldId == Areas);
        areas.IsMultiValueList.Should().BeTrue();
        areas.MultiListValues.Should().Equal(FirstOption);
        change.Clears.Should().BeEmpty();
        change.ValueChangeCount.Should().Be(2);
    }

    [Fact]
    public void PlanSave_SavesANewAnswer_ForAFieldThatHasNoRecordedValue()
    {
        var change = PlanSave(Answers(), [Answer(Endemic, boolean: false)]);

        var save = change.Saves.Should().ContainSingle().Subject;
        save.ProfileFieldId.Should().Be(Endemic);
        save.BooleanValue.Should().BeFalse();
    }

    [Fact]
    public void PlanSave_TreatsABlankTextAnswerAsNoAnswer()
    {
        PlanSave(Answers(Boolean(EndemicQuestion, Endemic, true)), [Answer(Endemic, boolean: true), Answer(Notes, text: string.Empty)])
            .ValueChangeCount.Should().Be(0);

        var change = PlanSave(
            Answers(Boolean(EndemicQuestion, Endemic, true), Text(DetailsQuestion, Notes, "Some notes")),
            [Answer(Endemic, boolean: true), Answer(Notes, text: null)]);

        var save = change.Saves.Should().ContainSingle().Subject;
        save.ProfileFieldId.Should().Be(Notes);
        save.TextValue.Should().BeNull();
    }

    [Fact]
    public void PlanSave_IgnoresADifferenceInLineEndings_ButNotInTheText()
    {
        var answers = Answers(Boolean(EndemicQuestion, Endemic, true), Text(DetailsQuestion, Notes, "one\ntwo"));

        PlanSave(answers, [Answer(Endemic, boolean: true), Answer(Notes, text: "one\r\ntwo")]).ValueChangeCount.Should().Be(0);
        PlanSave(answers, [Answer(Endemic, boolean: true), Answer(Notes, text: "one\r\nthree")]).ValueChangeCount.Should().Be(1);
    }

    [Fact]
    public void PlanSave_ComparesDatesByCalendarDay_AndDecimalsByValue_AndTreatsAnEmptyListValueAsNone()
    {
        var answers = Answers(
            new ReportFieldValue { QuestionId = EndemicQuestion, FieldId = Since, DateValue = new DateTime(2026, 1, 2, 10, 30, 0) },
            new ReportFieldValue { QuestionId = EndemicQuestion, FieldId = Count, DecimalValue = 1.5m },
            new ReportFieldValue { QuestionId = EndemicQuestion, FieldId = Level, ListValue = Guid.Empty },
            Boolean(EndemicQuestion, Endemic, true));

        var unchanged = PlanSave(
            answers,
            [
                Answer(Endemic, boolean: true),
                new() { ProfileFieldId = Since, DateValue = new DateTime(2026, 1, 2) },
                new() { ProfileFieldId = Count, DecimalValue = 1.50m },
                new() { ProfileFieldId = Level }
            ]);

        unchanged.ValueChangeCount.Should().Be(0);

        var changed = PlanSave(
            answers,
            [
                Answer(Endemic, boolean: true),
                new() { ProfileFieldId = Since, DateValue = new DateTime(2026, 1, 3) },
                new() { ProfileFieldId = Count, DecimalValue = 2m }
            ]);

        changed.Saves.Select(save => save.ProfileFieldId).Should().BeEquivalentTo([Since, Count]);
    }

    [Fact]
    public void PlanSave_ComparesAMultiValueListAsASet()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Option(DetailsQuestion, Areas, FirstOption),
            Option(DetailsQuestion, Areas, SecondOption));

        PlanSave(answers, [Answer(Endemic, boolean: true), Answer(Areas, options: [SecondOption, FirstOption, FirstOption])])
            .ValueChangeCount.Should().Be(0);

        var emptied = PlanSave(answers, [Answer(Endemic, boolean: true), Answer(Areas, options: [])]);

        emptied.Saves.Should().ContainSingle().Which.MultiListValues.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_IgnoresReadOnlyFields_ForTheKindOfProfile()
    {
        var answers = Answers(Boolean(EndemicQuestion, Endemic, true));
        ProfileFieldAnswerInputDto[] posted =
        [
            Answer(Endemic, boolean: true),
            Answer(Locked, text: "changed"),
            Answer(ScenarioLocked, text: "changed"),
            Answer(Open, text: "changed")
        ];

        PlanSave(answers, posted).Saves.Select(save => save.ProfileFieldId).Should().BeEquivalentTo([ScenarioLocked, Open]);
        PlanSave(answers, posted, isWhatIfScenario: true).Saves.Select(save => save.ProfileFieldId).Should().BeEquivalentTo([Locked, Open]);
    }

    [Fact]
    public void PlanSave_IgnoresAnAnswerForARepeatingQuestion_AndForUnknownFields_AndAPerSpeciesAnswerThatNamesNoSpecies()
    {
        var change = PlanSave(
            Answers(Boolean(EndemicQuestion, Endemic, true)),
            [Answer(Endemic, boolean: true), Answer(SpeciesNote, text: "x"), Answer(RowText, text: "x"), Answer(Guid.NewGuid(), text: "x")]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_SavesAPerSpeciesAnswerAgainstItsSpecies_OnlyWhenItDiffersFromWhatIsRecordedForThatSpecies()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, true, SpeciesA),
            Boolean(PresenceQuestion, Present, false, SpeciesB));

        PlanSave(answers, [Answer(Present, boolean: true, speciesId: SpeciesA), Answer(Present, boolean: false, speciesId: SpeciesB)])
            .ValueChangeCount.Should().Be(0);

        var change = PlanSave(answers, [Answer(Present, boolean: true, speciesId: SpeciesB)]);

        var save = change.Saves.Should().ContainSingle().Subject;
        save.ProfileFieldId.Should().Be(Present);
        save.SpeciesId.Should().Be(SpeciesB);
        save.BooleanValue.Should().BeTrue();
        change.Clears.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_KeepsEachSpeciesAnswerToTheSameFieldApart()
    {
        var answers = Answers(Boolean(PresenceQuestion, Present, false, SpeciesA), Boolean(PresenceQuestion, Present, false, SpeciesB));

        var change = PlanSave(answers, [Answer(Present, boolean: true, speciesId: SpeciesA), Answer(Present, boolean: true, speciesId: SpeciesB)]);

        change.Saves.Select(save => (save.ProfileFieldId, save.SpeciesId)).Should().BeEquivalentTo([(Present, (Guid?)SpeciesA), (Present, SpeciesB)]);
    }

    [Fact]
    public void PlanSave_ComparesAPerSpeciesMultiValueListAsASet_ForThatSpeciesOnly()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, true, SpeciesA),
            Boolean(PresenceQuestion, Present, true, SpeciesB),
            new ReportFieldValue { QuestionId = SpeciesDetailsQuestion, FieldId = SpeciesAreas, SpeciesId = SpeciesA, ListValue = FirstOption },
            new ReportFieldValue { QuestionId = SpeciesDetailsQuestion, FieldId = SpeciesAreas, SpeciesId = SpeciesA, ListValue = SecondOption });

        PlanSave(answers, [Answer(SpeciesAreas, options: [SecondOption, FirstOption], speciesId: SpeciesA)]).ValueChangeCount.Should().Be(0);

        var change = PlanSave(answers, [Answer(SpeciesAreas, options: [SecondOption, FirstOption], speciesId: SpeciesB)]);

        var save = change.Saves.Should().ContainSingle().Subject;
        save.SpeciesId.Should().Be(SpeciesB);
        save.IsMultiValueList.Should().BeTrue();
        save.MultiListValues.Should().BeEquivalentTo([FirstOption, SecondOption]);
    }

    [Fact]
    public void PlanSave_IgnoresAPerSpeciesAnswerForASpeciesTheVersionDoesNotProfile_AndAStandardAnswerThatNamesASpecies()
    {
        var answers = Answers(Boolean(EndemicQuestion, Endemic, true), Boolean(PresenceQuestion, Present, true, SpeciesA));

        var change = PlanSave(
            answers,
            [Answer(Present, boolean: false, speciesId: Guid.NewGuid()), Answer(Endemic, boolean: false, speciesId: SpeciesA)]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_IgnoresAPerSpeciesAnswerForAFieldThatIsReadOnlyForTheKindOfProfile()
    {
        var section = Section() with
        {
            Questions = [.. Section().Questions.Select(question => question.Id == PresenceQuestion ? question with { Fields = [Field(Present, "Boolean", readOnlyCurrent: true)] } : question)]
        };
        var answers = Answers(Boolean(EndemicQuestion, Endemic, true), Boolean(PresenceQuestion, Present, false, SpeciesA));

        var change = ProfileSectionChangePlanner.PlanSave(
            section,
            new Dictionary<Guid, ReportSectionAnswers> { [SectionId] = answers },
            isWhatIfScenario: false,
            [Answer(Present, boolean: true, speciesId: SpeciesA)],
            postedRows: [],
            lastUpdated: null);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_ClearsTheQuestionsAPerSpeciesAnswerHasMadeIrrelevantForThatSpecies_AndDropsAnswersPostedForThem()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, true, SpeciesA),
            Boolean(PresenceQuestion, Present, true, SpeciesB),
            Text(SpeciesDetailsQuestion, SpeciesNote, "Cattle detail", SpeciesA),
            Text(SpeciesDetailsQuestion, SpeciesNote, "Sheep detail", SpeciesB));

        var change = PlanSave(
            answers,
            [Answer(Present, boolean: false, speciesId: SpeciesA), Answer(SpeciesNote, text: "Typed just now", speciesId: SpeciesA)]);

        change.Saves.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { ProfileFieldId = Present, SpeciesId = (Guid?)SpeciesA, BooleanValue = (bool?)false });
        change.Clears.Should().BeEquivalentTo([Clear(SpeciesNote, speciesId: SpeciesA)]);
    }

    [Fact]
    public void PlanSave_ClearsTheQuestionsAnAnswerHasMadeIrrelevant_CascadingToTheQuestionsTheyControl_AndDeletesTheirRows()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Text(DetailsQuestion, Notes, "Some notes"),
            Option(DetailsQuestion, Areas, FirstOption),
            Option(DetailsQuestion, Areas, SecondOption),
            Boolean(DetailsQuestion, Confirmed, true),
            Text(FollowUpQuestion, FollowUp, "Follow up"),
            Text(RowsQuestion, RowText, "Row one", rowId: RowOne),
            Text(RowsQuestion, RowText, "Row two", rowId: RowTwo),
            Text(LockedQuestion, Locked, "Locked text"),
            Text(LockedQuestion, Open, "Open text"));

        var change = PlanSave(answers, [Answer(Endemic, boolean: false)], lastUpdated: RowVersion);

        change.Saves.Should().ContainSingle().Which.ProfileFieldId.Should().Be(Endemic);
        change.Clears.Should().BeEquivalentTo(
            [Clear(Notes), Clear(Areas, isMultiValueList: true), Clear(Confirmed), Clear(FollowUp), Clear(Open)]);
        change.QuestionRowIdsToDelete.Should().BeEquivalentTo([RowOne, RowTwo]);
        change.ExpectedLastUpdated.Should().Equal(RowVersion);
        change.ClearRevision.Should().BeFalse();
    }

    [Fact]
    public void PlanSave_DropsAPostedAnswer_ForAQuestionTheSameSaveMakesIrrelevant()
    {
        var answers = Answers(Boolean(EndemicQuestion, Endemic, true), Text(DetailsQuestion, Notes, "Some notes"));

        var change = PlanSave(answers, [Answer(Endemic, boolean: false), Answer(Notes, text: "Typed just now"), Answer(Open, text: "New")]);

        change.Saves.Select(save => save.ProfileFieldId).Should().BeEquivalentTo([Endemic]);
        change.Clears.Should().BeEquivalentTo([Clear(Notes)]);
    }

    [Fact]
    public void PlanSave_ClearsNothing_ForAnIrrelevantQuestionWithNoRecordedValue()
    {
        var change = PlanSave(Answers(Boolean(EndemicQuestion, Endemic, true)), [Answer(Endemic, boolean: false), Answer(Notes, text: "Typed just now")]);

        change.Saves.Select(save => save.ProfileFieldId).Should().BeEquivalentTo([Endemic]);
        change.Clears.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_ClearsAStaleIrrelevantQuestion_EvenWhenNothingWasPosted()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, false),
            Text(DetailsQuestion, Notes, "Some notes"),
            Boolean(DetailsQuestion, Confirmed, true),
            Text(FollowUpQuestion, FollowUp, "Follow up"));

        var change = PlanSave(answers, []);

        change.Saves.Should().BeEmpty();
        change.Clears.Should().BeEquivalentTo([Clear(Notes), Clear(Confirmed), Clear(FollowUp)]);
        change.QuestionRowIdsToDelete.Should().BeEquivalentTo([RowOne, RowTwo]);
        change.ValueChangeCount.Should().Be(5);
    }

    [Fact]
    public void PlanSave_ClearsAPerSpeciesQuestion_OnlyForTheSpeciesItIsIrrelevantFor()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, true, SpeciesA),
            Boolean(PresenceQuestion, Present, false, SpeciesB),
            Text(SpeciesDetailsQuestion, SpeciesNote, "Cattle detail", SpeciesA),
            Text(SpeciesDetailsQuestion, SpeciesNote, "Sheep detail", SpeciesB));

        var change = PlanSave(answers, [Answer(Endemic, boolean: true)]);

        change.Clears.Should().BeEquivalentTo([Clear(SpeciesNote, speciesId: SpeciesB)]);
        change.Saves.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_ConsidersTheAnswersOfOtherSections_WhenDecidingRelevance()
    {
        var otherSectionId = Guid.NewGuid();
        var otherAnswers = new ReportSectionAnswers { Values = [Boolean(Guid.NewGuid(), Endemic, true)] };
        var thisAnswers = Answers(Text(DetailsQuestion, Notes, "Some notes"));

        var change = ProfileSectionChangePlanner.PlanSave(
            Section(),
            new Dictionary<Guid, ReportSectionAnswers> { [SectionId] = thisAnswers, [otherSectionId] = otherAnswers },
            isWhatIfScenario: false,
            [],
            postedRows: [],
            lastUpdated: null);

        change.ValueChangeCount.Should().Be(0);
    }

    private static ProfileRepeatingQuestionInputDto RowsOf(params ProfileQuestionRowInputDto[] rows) => new() { QuestionId = RowsQuestion, Rows = rows };

    private static ProfileQuestionRowInputDto Row(Guid id, string? text = null) => new() { Id = id, Answers = [Answer(RowText, text: text)] };

    // The Rows question is relevant while Endemic is Yes, and holds two recorded rows with one answer each.
    private static ReportSectionAnswers RowAnswers() => Answers(
        Boolean(EndemicQuestion, Endemic, true),
        Text(RowsQuestion, RowText, "Row one", rowId: RowOne),
        Text(RowsQuestion, RowText, "Row two", rowId: RowTwo));

    [Fact]
    public void PlanSave_SavesTheAnswerOfARowThatChanged_AgainstTheRow_AndLeavesTheOthersAlone()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowOne, "Changed"), Row(RowTwo, "Row two"))]);

        change.Saves.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { ProfileFieldId = RowText, ProfileVersionQuestionRowId = (Guid?)RowOne, SpeciesId = (Guid?)null, TextValue = "Changed" });
        change.QuestionRowsToInsert.Should().BeEmpty();
        change.QuestionRowsToUpdate.Should().BeEmpty();
        change.QuestionRowIdsToDelete.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_AddsANewRowAfterTheRecordedOnes_AndSavesItsAnswers()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowOne, "Row one"), Row(RowTwo, "Row two"), Row(NewRow, "Row three"))]);

        change.QuestionRowsToInsert.Should().ContainSingle().Which.Should().Be(new ProfileQuestionRowInstruction(NewRow, RowsQuestion, 2));
        change.Saves.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { ProfileFieldId = RowText, ProfileVersionQuestionRowId = (Guid?)NewRow, TextValue = "Row three" });
        change.QuestionRowsToUpdate.Should().BeEmpty();
        change.ValueChangeCount.Should().Be(2);
    }

    [Fact]
    public void PlanSave_AddsANewRowWithNoAnswers_WithoutSavingAnything()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowOne, "Row one"), Row(RowTwo, "Row two"), Row(NewRow))]);

        change.QuestionRowsToInsert.Should().ContainSingle().Which.Id.Should().Be(NewRow);
        change.Saves.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_DeletesARecordedRowThatIsNoLongerPosted_AndNumbersTheRowsThatRemain()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowTwo, "Row two"))]);

        change.QuestionRowIdsToDelete.Should().BeEquivalentTo([RowOne]);
        change.QuestionRowsToUpdate.Should().ContainSingle().Which.Should().Be(new ProfileQuestionRowInstruction(RowTwo, RowsQuestion, 0));
        change.Saves.Should().BeEmpty();
        change.QuestionRowsToInsert.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_DeletesEveryRow_WhenTheQuestionIsPostedWithNone()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf()]);

        change.QuestionRowIdsToDelete.Should().BeEquivalentTo([RowOne, RowTwo]);
        change.QuestionRowsToUpdate.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_NumbersTheRowsInTheOrderTheyArePosted()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowTwo, "Row two"), Row(RowOne, "Row one"))]);

        change.QuestionRowsToUpdate.Should().BeEquivalentTo(
            [new ProfileQuestionRowInstruction(RowTwo, RowsQuestion, 0), new ProfileQuestionRowInstruction(RowOne, RowsQuestion, 1)]);
        change.QuestionRowIdsToDelete.Should().BeEmpty();
        change.Saves.Should().BeEmpty();
    }

    [Fact]
    public void PlanSave_LeavesTheRowsOfAQuestionAlone_WhenTheSaveDoesNotListIt()
    {
        var change = PlanSave(RowAnswers(), []);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_ChangesNoRow_WhenThePostedRowsMatchTheRecordedOnes()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowOne, "Row one"), Row(RowTwo, "Row two"))]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_TakesTheLastListingOfAQuestion_WhenItIsListedMoreThanOnce()
    {
        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowOne, "Row one")), RowsOf(Row(RowOne, "Row one"), Row(RowTwo, "Row two"))]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_IgnoresARowWhoseIdentifierIsEmptyRepeatedOrBelongsToAnotherQuestion()
    {
        var answers = RowAnswers() with
        {
            Rows = [new ReportQuestionRow(RowOne, RowsQuestion, 0), new ReportQuestionRow(RowTwo, RowsQuestion, 1), new ReportQuestionRow(OtherQuestionRow, DetailsQuestion, 0)]
        };

        var change = PlanSave(
            answers,
            [],
            postedRows: [RowsOf(Row(RowOne, "Row one"), Row(Guid.Empty, "Empty"), Row(RowOne, "Repeated"), Row(OtherQuestionRow, "Taken"), Row(RowTwo, "Row two"))]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_IgnoresAnAnswerPostedForAFieldThatIsNotInTheRow_OrNotInTheQuestion()
    {
        var change = PlanSave(
            RowAnswers(),
            [],
            postedRows: [RowsOf(new ProfileQuestionRowInputDto { Id = RowOne, Answers = [Answer(Notes, text: "Not a field of the rows"), Answer(Guid.NewGuid(), text: "Unknown")] }, Row(RowTwo, "Row two"))]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_IgnoresAListingOfAQuestionThatIsNotRepeating_OrIsNotInTheSection()
    {
        var change = PlanSave(
            RowAnswers(),
            [],
            postedRows:
            [
                new ProfileRepeatingQuestionInputDto { QuestionId = DetailsQuestion, Rows = [Row(NewRow, "Not a repeating question")] },
                new ProfileRepeatingQuestionInputDto { QuestionId = Guid.NewGuid(), Rows = [Row(NewRow, "No such question")] }
            ]);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_ChangesNoRow_ForAQuestionWhoseFieldsAreAllReadOnlyForTheKindOfProfile()
    {
        var section = Section() with
        {
            Questions = [.. Section().Questions.Select(question => question.Id == RowsQuestion ? question with { Fields = [Field(RowText, "Text", readOnlyCurrent: true)] } : question)]
        };

        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(Row(RowOne, "Changed"))], section: section);

        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void PlanSave_LeavesAReadOnlyFieldOfARowAlone_WhileSavingTheOthers()
    {
        var section = Section() with
        {
            Questions = [.. Section().Questions.Select(question => question.Id == RowsQuestion
                ? question with { Fields = [Field(RowText, "Text", readOnlyCurrent: true), Field(RowAreas, "MultiValueList")] }
                : question)]
        };
        ProfileQuestionRowInputDto row = new() { Id = RowOne, Answers = [Answer(RowText, text: "Changed"), Answer(RowAreas, options: [FirstOption])] };

        var change = PlanSave(RowAnswers(), [], postedRows: [RowsOf(row, Row(RowTwo, "Row two"))], section: section);

        change.Saves.Should().ContainSingle().Which.ProfileFieldId.Should().Be(RowAreas);
    }

    [Fact]
    public void PlanSave_SavesTheItemsOfAMultiValueListInARow_AgainstTheRow()
    {
        var section = Section() with
        {
            Questions = [.. Section().Questions.Select(question => question.Id == RowsQuestion
                ? question with { Fields = [Field(RowText, "Text"), Field(RowAreas, "MultiValueList")] }
                : question)]
        };
        var answers = RowAnswers() with
        {
            Values = [.. RowAnswers().Values, new ReportFieldValue { QuestionId = RowsQuestion, FieldId = RowAreas, ListValue = FirstOption, RowId = RowOne }]
        };
        ProfileQuestionRowInputDto row = new() { Id = RowOne, Answers = [Answer(RowText, text: "Row one"), Answer(RowAreas, options: [SecondOption, FirstOption])] };

        var change = PlanSave(answers, [], postedRows: [RowsOf(row, Row(RowTwo, "Row two"))], section: section);

        var save = change.Saves.Should().ContainSingle().Which;
        save.ProfileFieldId.Should().Be(RowAreas);
        save.ProfileVersionQuestionRowId.Should().Be(RowOne);
        save.IsMultiValueList.Should().BeTrue();
        save.MultiListValues.Should().BeEquivalentTo([FirstOption, SecondOption]);
    }

    [Fact]
    public void PlanSave_DropsThePostedRows_OfAQuestionTheSameSaveMakesIrrelevant()
    {
        var change = PlanSave(
            RowAnswers(),
            [Answer(Endemic, boolean: false)],
            postedRows: [RowsOf(Row(RowOne, "Changed"), Row(NewRow, "New row"))]);

        change.QuestionRowIdsToDelete.Should().BeEquivalentTo([RowOne, RowTwo]);
        change.QuestionRowsToInsert.Should().BeEmpty();
        change.QuestionRowsToUpdate.Should().BeEmpty();
        change.Saves.Should().ContainSingle().Which.ProfileFieldId.Should().Be(Endemic);
    }

    [Fact]
    public void PlanClear_ClearsEveryRecordedValueThatIsNotReadOnly_DeletesEveryRow_AndBlanksTheRevisionData()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Text(DetailsQuestion, Notes, "Some notes"),
            Option(DetailsQuestion, Areas, FirstOption),
            Option(DetailsQuestion, Areas, SecondOption),
            Text(SpeciesDetailsQuestion, SpeciesNote, "Cattle detail", SpeciesA),
            Text(RowsQuestion, RowText, "Row one", rowId: RowOne),
            Text(LockedQuestion, Locked, "Locked text"),
            Text(LockedQuestion, ScenarioLocked, "Scenario locked text"));

        var change = ProfileSectionChangePlanner.PlanClear(Section(), answers, isWhatIfScenario: false, RowVersion);

        change.Clears.Should().BeEquivalentTo(
            [Clear(Endemic), Clear(Notes), Clear(Areas, isMultiValueList: true), Clear(SpeciesNote, speciesId: SpeciesA), Clear(ScenarioLocked)]);
        change.QuestionRowIdsToDelete.Should().BeEquivalentTo([RowOne, RowTwo]);
        change.ClearRevision.Should().BeTrue();
        change.ExpectedLastUpdated.Should().Equal(RowVersion);
        change.Saves.Should().BeEmpty();
    }

    [Fact]
    public void PlanClear_LeavesTheFieldsThatAreReadOnlyForAScenario_WhenTheProfileIsAScenario()
    {
        var answers = Answers(Text(LockedQuestion, Locked, "Locked text"), Text(LockedQuestion, ScenarioLocked, "Scenario locked text"));

        var change = ProfileSectionChangePlanner.PlanClear(Section(), answers, isWhatIfScenario: true, lastUpdated: null);

        change.Clears.Should().BeEquivalentTo([Clear(Locked)]);
        change.ExpectedLastUpdated.Should().BeNull();
    }

    [Fact]
    public void PlanClear_StillBlanksTheRevisionData_WhenNothingIsRecorded()
    {
        var change = ProfileSectionChangePlanner.PlanClear(Section(), new ReportSectionAnswers(), isWhatIfScenario: false, lastUpdated: null);

        change.Clears.Should().BeEmpty();
        change.ClearRevision.Should().BeTrue();
        change.ValueChangeCount.Should().Be(0);
    }

    [Fact]
    public void FindIrrelevantQuestionIds_ListsTheQuestionsTheAnswersMakeIrrelevant()
    {
        var answers = Answers(Boolean(EndemicQuestion, Endemic, false), Boolean(PresenceQuestion, Present, true, SpeciesA));

        var ids = ProfileSectionChangePlanner.FindIrrelevantQuestionIds(Section(), answers, answers.Values.ToLookup(value => value.FieldId));

        ids.Should().BeEquivalentTo([DetailsQuestion, FollowUpQuestion, RowsQuestion, LockedQuestion]);
    }

    [Fact]
    public void FindIrrelevantQuestionIds_ListsAPerSpeciesQuestion_OnlyWhenItIsIrrelevantForEveryProfiledSpecies()
    {
        var allAnswers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, false, SpeciesA),
            Boolean(PresenceQuestion, Present, false, SpeciesB));
        var oneRelevant = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, false, SpeciesA),
            Boolean(PresenceQuestion, Present, true, SpeciesB));

        ProfileSectionChangePlanner.FindIrrelevantQuestionIds(Section(), allAnswers, allAnswers.Values.ToLookup(value => value.FieldId))
            .Should().Contain(SpeciesDetailsQuestion);
        ProfileSectionChangePlanner.FindIrrelevantQuestionIds(Section(), oneRelevant, oneRelevant.Values.ToLookup(value => value.FieldId))
            .Should().NotContain(SpeciesDetailsQuestion);
    }

    [Fact]
    public void FindIrrelevantQuestionIds_ListsNothing_ForAQuestionWithNoRelevanceRule()
    {
        var answers = new ReportSectionAnswers();

        var ids = ProfileSectionChangePlanner.FindIrrelevantQuestionIds(Section(), answers, answers.Values.ToLookup(value => value.FieldId));

        ids.Should().NotContain([EndemicQuestion, PresenceQuestion]);
    }

    [Fact]
    public void FindIrrelevantSpecies_ListsEachSpeciesAPerSpeciesQuestionIsIrrelevantFor_EvenWhenItIsRelevantForAnother()
    {
        var answers = Answers(
            Boolean(EndemicQuestion, Endemic, true),
            Boolean(PresenceQuestion, Present, true, SpeciesA),
            Boolean(PresenceQuestion, Present, false, SpeciesB));

        var species = ProfileSectionChangePlanner.FindIrrelevantSpecies(Section(), answers, answers.Values.ToLookup(value => value.FieldId));

        species.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProfileQuestionSpeciesDto { QuestionId = SpeciesDetailsQuestion, SpeciesId = SpeciesB });
    }

    [Fact]
    public void FindIrrelevantSpecies_ListsEveryProfiledSpecies_WhenTheControllingFieldIsUnanswered()
    {
        var answers = Answers();

        var species = ProfileSectionChangePlanner.FindIrrelevantSpecies(Section(), answers, answers.Values.ToLookup(value => value.FieldId));

        species.Select(item => item.SpeciesId).Should().BeEquivalentTo([SpeciesA, SpeciesB]);
        species.Should().OnlyContain(item => item.QuestionId == SpeciesDetailsQuestion);
    }

    [Fact]
    public void FindIrrelevantSpecies_ListsNothing_ForAStandardQuestion_OrAPerSpeciesQuestionWithNoRelevanceRule()
    {
        var answers = Answers(Boolean(EndemicQuestion, Endemic, false));

        var species = ProfileSectionChangePlanner.FindIrrelevantSpecies(Section(), answers, answers.Values.ToLookup(value => value.FieldId));

        species.Should().NotContain(item => item.QuestionId == PresenceQuestion || item.QuestionId == DetailsQuestion);
    }
}
