using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileSections;

/// <summary>
/// Works out what a save or a clear of one profile version section has to write, replicating the legacy
/// <c>EditProfileQuestions</c> page. A save writes only the answers that changed, after first clearing the questions that
/// have become irrelevant (<c>HideAndClearIrrelevantQuestion</c>); a clear removes every answer, every repeating row and the
/// section's revision data (<c>ProfileVersionSection.Clear()</c>). A field that is read-only for the kind of profile is never
/// changed (<c>FieldValueBase.Clear()</c> skips it and the page does not save it).
/// </summary>
internal sealed class ProfileSectionChangePlanner
{
    private const string MultiValueListDataType = "MultiValueList";

    private readonly ReportSection section;
    private readonly ReportSectionAnswers sectionAnswers;
    private readonly bool isWhatIfScenario;
    private readonly Dictionary<Guid, FieldEntry> fields;
    private readonly Dictionary<Guid, FieldEntry> editableFields;
    private readonly Dictionary<Guid, FieldEntry> rowFields;
    private readonly Dictionary<Guid, Guid> questionOfRow;
    private readonly List<ReportFieldValue> recorded;
    private readonly List<ReportFieldValue> working;
    private readonly Dictionary<(Guid FieldId, Guid? SpeciesId, Guid? RowId), ProfileFieldSaveInstruction> saves = [];
    private readonly HashSet<(Guid FieldId, Guid? SpeciesId)> cleared = [];
    private readonly HashSet<Guid> deletedRows = [];
    private readonly List<ProfileQuestionRowInstruction> rowInserts = [];
    private readonly List<ProfileQuestionRowInstruction> rowUpdates = [];
    private ILookup<Guid, ReportFieldValue>? valuesByField;

    private ProfileSectionChangePlanner(
        ReportSection section,
        IReadOnlyDictionary<Guid, ReportSectionAnswers> answersBySection,
        bool isWhatIfScenario)
    {
        this.section = section;
        this.isWhatIfScenario = isWhatIfScenario;
        sectionAnswers = answersBySection[section.Id];
        fields = FieldsOf(section);

        // Only the answers to a standard question, or to a per-species question for one of the version's profiled species, are edited
        // through the page as answers in their own right. A field group is a container for a repeating question's nested fields, not an
        // answerable field.
        editableFields = section.Questions
            .Where(question => !question.IsRepeating)
            .SelectMany(question => question.Fields.Select(field => new FieldEntry(question, field)))
            .Where(entry => IsChangeable(entry.Field, isWhatIfScenario))
            .ToDictionary(entry => entry.Field.Id);

        // The answers to a repeating question are edited row by row.
        rowFields = section.Questions
            .Where(question => question.IsRepeating)
            .SelectMany(question => question.Fields.Select(field => new FieldEntry(question, field)))
            .Where(entry => IsChangeable(entry.Field, isWhatIfScenario))
            .ToDictionary(entry => entry.Field.Id);

        // A row's identifier belongs to one question of one section, so a new row may not take the identifier of another's.
        questionOfRow = answersBySection.Values.SelectMany(answers => answers.Rows).ToDictionary(row => row.Id, row => row.QuestionId);

        // A question's relevance can hang on an answer in another section, so every section's answers are considered.
        recorded = [.. answersBySection.Values.SelectMany(answers => answers.Values)];
        working = [.. recorded];
    }

    /// <summary>Plans a save: the posted answers that differ from what is recorded, the posted rows of the repeating questions, then the clearing of what the answers have made irrelevant.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answersBySection">Every section's recorded answers, by section.</param>
    /// <param name="isWhatIfScenario">Whether the profile is a "what-if" scenario, which has its own read-only fields.</param>
    /// <param name="posted">The answers submitted for the section.</param>
    /// <param name="postedRows">The rows submitted for the section's repeating questions; a question that is not listed keeps the rows it has.</param>
    /// <param name="lastUpdated">The row version of the section the caller last read, or <see langword="null"/>.</param>
    /// <returns>The change to apply; it has no value changes when the answers equal what is recorded and nothing is irrelevant.</returns>
    public static ProfileSectionChange PlanSave(
        ReportSection section,
        IReadOnlyDictionary<Guid, ReportSectionAnswers> answersBySection,
        bool isWhatIfScenario,
        IReadOnlyList<ProfileFieldAnswerInputDto> posted,
        IReadOnlyList<ProfileRepeatingQuestionInputDto> postedRows,
        byte[]? lastUpdated)
    {
        var planner = new ProfileSectionChangePlanner(section, answersBySection, isWhatIfScenario);

        planner.ApplyPostedAnswers(posted);
        planner.ApplyPostedRows(postedRows);
        planner.ClearIrrelevantQuestions();

        return planner.ToChange(lastUpdated);
    }

    /// <summary>Plans a clear: every recorded value that is not read-only, every repeating row, and the section's revision data.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answers">The section's recorded answers.</param>
    /// <param name="isWhatIfScenario">Whether the profile is a "what-if" scenario, which has its own read-only fields.</param>
    /// <param name="lastUpdated">The row version of the section the caller last read, or <see langword="null"/>.</param>
    /// <returns>The change to apply.</returns>
    public static ProfileSectionChange PlanClear(
        ReportSection section,
        ReportSectionAnswers answers,
        bool isWhatIfScenario,
        byte[]? lastUpdated)
    {
        var fields = FieldsOf(section);

        // A value in a repeating row goes with the row, so only the values outside rows are cleared one by one.
        var clears = answers.Values
            .Where(value => value.RowId is null && fields.TryGetValue(value.FieldId, out var entry) && IsChangeable(entry.Field, isWhatIfScenario))
            .Select(value => ToClearInstruction(value, fields[value.FieldId].Field))
            .Distinct()
            .ToList();

        return new ProfileSectionChange
        {
            Clears = clears,
            QuestionRowIdsToDelete = [.. answers.Rows.Select(row => row.Id)],
            ClearRevision = true,
            ExpectedLastUpdated = lastUpdated
        };
    }

    /// <summary>Finds the questions the page hides because their answers make them irrelevant, replicating the legacy
    /// <c>HideAndClearIrrelevantQuestion</c>: a question is hidden when it is irrelevant, and a per-species question when it
    /// is irrelevant for every profiled species.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answers">The section's recorded answers.</param>
    /// <param name="valuesByField">Every recorded value in the profile version, by field.</param>
    /// <returns>The identifiers of the hidden questions.</returns>
    public static IReadOnlyList<Guid> FindIrrelevantQuestionIds(
        ReportSection section,
        ReportSectionAnswers answers,
        ILookup<Guid, ReportFieldValue> valuesByField) =>
        [.. section.Questions
            .Where(question => question.IrrelevanceFieldId != Guid.Empty)
            .Where(question => question.IsPerSpecies
                ? !answers.ProfiledSpecies.Any(species => QuestionRelevance.IsRelevant(question, species.Id, valuesByField))
                : !QuestionRelevance.IsRelevant(question, null, valuesByField))
            .Select(question => question.Id)];

    /// <summary>Finds the species a per-species question is irrelevant for, replicating the legacy <c>QuestionSpecies.IsRelevant</c>: the
    /// page leaves such a species out of the question's table, and a save clears the question's answers for it.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answers">The section's recorded answers, which name the profiled species.</param>
    /// <param name="valuesByField">Every recorded value in the profile version, by field.</param>
    /// <returns>Each question and species it does not apply to. A question with no relevance rule applies to every species.</returns>
    public static IReadOnlyList<ProfileQuestionSpeciesDto> FindIrrelevantSpecies(
        ReportSection section,
        ReportSectionAnswers answers,
        ILookup<Guid, ReportFieldValue> valuesByField) =>
        [.. section.Questions
            .Where(question => question.IsPerSpecies && question.IrrelevanceFieldId != Guid.Empty)
            .SelectMany(question => answers.ProfiledSpecies
                .Where(species => !QuestionRelevance.IsRelevant(question, species.Id, valuesByField))
                .Select(species => new ProfileQuestionSpeciesDto { QuestionId = question.Id, SpeciesId = species.Id }))];

    private static Dictionary<Guid, FieldEntry> FieldsOf(ReportSection section) =>
        section.Questions
            .SelectMany(question => question.Fields
                .SelectMany(field => field.Children.Prepend(field))
                .Select(field => new FieldEntry(question, field)))
            .ToDictionary(entry => entry.Field.Id);

    private static bool IsChangeable(ReportField field, bool isWhatIfScenario) =>
        field.DataTypeName != ReportField.FieldGroupDataType
        && !(isWhatIfScenario ? field.IsReadOnlyScenario : field.IsReadOnlyCurrentSituation);

    private static bool IsMultiValueList(ReportField field) =>
        string.Equals(field.DataTypeName, MultiValueListDataType, StringComparison.OrdinalIgnoreCase);

    private static bool Has(Guid? id) => id is { } value && value != Guid.Empty;

    private static Guid? NormaliseList(Guid? id) => Has(id) ? id : null;

    private static string? NormaliseText(string? text) => string.IsNullOrEmpty(text) ? null : text;

    private static ProfileFieldClearInstruction ToClearInstruction(ReportFieldValue value, ReportField field) => new()
    {
        ProfileFieldId = value.FieldId,
        SpeciesId = value.SpeciesId,
        IsMultiValueList = IsMultiValueList(field)
    };

    private static bool Differs(ReportField field, List<ReportFieldValue> existing, ProfileFieldAnswerInputDto answer)
    {
        if (IsMultiValueList(field))
        {
            var before = existing.Where(value => Has(value.ListValue)).Select(value => value.ListValue!.Value).ToHashSet();

            return !before.SetEquals(answer.MultiListValues.Where(id => id != Guid.Empty));
        }

        var current = existing.Count > 0 ? existing[0] : null;

        return current?.BooleanValue != answer.BooleanValue
            || NormaliseList(current?.ListValue) != NormaliseList(answer.ListValue)
            || current?.DecimalValue != answer.DecimalValue
            || current?.DateValue?.Date != answer.DateValue?.Date
            || ComparableText(current?.TextValue) != ComparableText(answer.TextValue);
    }

    /// <summary>A browser posts a text area's line breaks as CRLF whatever was stored, which is not a change to the text.</summary>
    private static string? ComparableText(string? text) => NormaliseText(text)?.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static ProfileFieldSaveInstruction ToSaveInstruction(ReportField field, ProfileFieldAnswerInputDto answer, Guid? rowId = null) => new()
    {
        ProfileFieldId = field.Id,
        SpeciesId = rowId is null ? answer.SpeciesId : null,
        ProfileVersionQuestionRowId = rowId,
        BooleanValue = answer.BooleanValue,
        ListValue = NormaliseList(answer.ListValue),
        MultiListValues = [.. answer.MultiListValues.Where(id => id != Guid.Empty).Distinct()],
        DecimalValue = answer.DecimalValue,
        DateValue = answer.DateValue,
        TextValue = NormaliseText(answer.TextValue),
        IsMultiValueList = IsMultiValueList(field)
    };

    private static IEnumerable<ReportFieldValue> ToValues(FieldEntry entry, ProfileFieldSaveInstruction save)
    {
        if (save.IsMultiValueList)
        {
            return save.MultiListValues.Select(listValue => new ReportFieldValue
            {
                QuestionId = entry.Question.Id,
                FieldId = entry.Field.Id,
                SpeciesId = save.SpeciesId,
                RowId = save.ProfileVersionQuestionRowId,
                ListValue = listValue
            });
        }

        var hasValue = save.BooleanValue is not null || save.ListValue is not null || save.DecimalValue is not null || save.DateValue is not null || save.TextValue is not null;

        return hasValue
            ? [new ReportFieldValue
            {
                QuestionId = entry.Question.Id,
                FieldId = entry.Field.Id,
                SpeciesId = save.SpeciesId,
                RowId = save.ProfileVersionQuestionRowId,
                BooleanValue = save.BooleanValue,
                ListValue = save.ListValue,
                DecimalValue = save.DecimalValue,
                DateValue = save.DateValue,
                TextValue = save.TextValue
            }]
            : [];
    }

    private static bool IsDirectValue(ReportFieldValue value, Guid fieldId, Guid? speciesId) =>
        value.FieldId == fieldId && value.SpeciesId == speciesId && value.RowId is null;

    private static bool IsRowValue(ReportFieldValue value, Guid fieldId, Guid rowId) =>
        value.FieldId == fieldId && value.RowId == rowId;

    private ILookup<Guid, ReportFieldValue> ValuesByField => valuesByField ??= working.ToLookup(value => value.FieldId);

    /// <summary>A standard question is answered without a species. A per-species question is answered once for each species the
    /// version profiles, and an answer for any other species is not one the page offered.</summary>
    private bool IsAnswerableFor(ReportQuestion question, Guid? speciesId) =>
        question.IsPerSpecies
            ? speciesId is { } id && sectionAnswers.ProfiledSpecies.Any(species => species.Id == id)
            : speciesId is null;

    private void ApplyPostedAnswers(IReadOnlyList<ProfileFieldAnswerInputDto> posted)
    {
        foreach (var answer in posted)
        {
            if (!editableFields.TryGetValue(answer.ProfileFieldId, out var entry) || !IsAnswerableFor(entry.Question, answer.SpeciesId))
            {
                continue;
            }

            var existing = working.Where(value => IsDirectValue(value, entry.Field.Id, answer.SpeciesId)).ToList();

            if (!Differs(entry.Field, existing, answer))
            {
                continue;
            }

            var save = ToSaveInstruction(entry.Field, answer);
            saves[(entry.Field.Id, answer.SpeciesId, null)] = save;
            working.RemoveAll(value => IsDirectValue(value, entry.Field.Id, answer.SpeciesId));
            working.AddRange(ToValues(entry, save));
            valuesByField = null;
        }
    }

    /// <summary>Applies the rows posted for each listed repeating question, which stand for all the question's rows: a recorded row that is
    /// not among them is deleted, a row that is not recorded is added, and the rows are numbered in the order posted, as the legacy
    /// <c>QuestionRowList</c> numbered them.</summary>
    private void ApplyPostedRows(IReadOnlyList<ProfileRepeatingQuestionInputDto> postedRows)
    {
        // A question listed twice is taken as its last listing says.
        foreach (var posted in postedRows.GroupBy(item => item.QuestionId).Select(group => group.Last()))
        {
            var question = section.Questions.FirstOrDefault(candidate => candidate.Id == posted.QuestionId && candidate.IsRepeating);

            // A question none of whose fields can be changed has no rows to change.
            if (question is not null && question.Fields.Any(field => rowFields.ContainsKey(field.Id)))
            {
                ApplyPostedRows(question, posted.Rows);
            }
        }
    }

    private void ApplyPostedRows(ReportQuestion question, IReadOnlyList<ProfileQuestionRowInputDto> postedRows)
    {
        var recordedRows = sectionAnswers.Rows.Where(row => row.QuestionId == question.Id).OrderBy(row => row.SequenceNumber).ToList();
        var recordedIds = recordedRows.Select(row => row.Id).ToHashSet();
        var seen = new HashSet<Guid>();

        // A row is a recorded row of the question, or a new one, whose identifier no recorded row of any question has. An identifier the
        // page repeats or leaves empty is not a row.
        var kept = postedRows
            .Where(row => row.Id != Guid.Empty && (recordedIds.Contains(row.Id) || !questionOfRow.ContainsKey(row.Id)) && seen.Add(row.Id))
            .ToList();

        foreach (var goneId in recordedRows.Where(row => !seen.Contains(row.Id)).Select(row => row.Id))
        {
            deletedRows.Add(goneId);
            working.RemoveAll(value => value.RowId == goneId);
            valuesByField = null;
        }

        for (var position = 0; position < kept.Count; position++)
        {
            var recordedRow = recordedRows.FirstOrDefault(row => row.Id == kept[position].Id);

            if (recordedRow is null)
            {
                rowInserts.Add(new ProfileQuestionRowInstruction(kept[position].Id, question.Id, position));
            }
            else if (recordedRow.SequenceNumber != position)
            {
                rowUpdates.Add(new ProfileQuestionRowInstruction(recordedRow.Id, question.Id, position));
            }

            ApplyPostedRowAnswers(question, kept[position]);
        }
    }

    private void ApplyPostedRowAnswers(ReportQuestion question, ProfileQuestionRowInputDto row)
    {
        foreach (var answer in row.Answers)
        {
            if (!rowFields.TryGetValue(answer.ProfileFieldId, out var entry) || entry.Question.Id != question.Id)
            {
                continue;
            }

            var existing = working.Where(value => IsRowValue(value, entry.Field.Id, row.Id)).ToList();

            if (!Differs(entry.Field, existing, answer))
            {
                continue;
            }

            var save = ToSaveInstruction(entry.Field, answer, row.Id);
            saves[(entry.Field.Id, null, row.Id)] = save;
            working.RemoveAll(value => IsRowValue(value, entry.Field.Id, row.Id));
            working.AddRange(ToValues(entry, save));
            valuesByField = null;
        }
    }

    /// <summary>One pass over the section's questions in order, as the legacy page made: clearing a question's answers can make
    /// a later question irrelevant, which the same pass then clears.</summary>
    private void ClearIrrelevantQuestions()
    {
        foreach (var question in section.Questions.Where(question => question.IrrelevanceFieldId != Guid.Empty))
        {
            if (question.IsRepeating)
            {
                if (!QuestionRelevance.IsRelevant(question, null, ValuesByField))
                {
                    DeleteRows(question);
                }
            }
            else if (question.IsPerSpecies)
            {
                foreach (var speciesId in SpeciesOf(question).Where(speciesId => !QuestionRelevance.IsRelevant(question, speciesId, ValuesByField)))
                {
                    ClearValues(question, speciesId);
                }
            }
            else if (!QuestionRelevance.IsRelevant(question, null, ValuesByField))
            {
                ClearValues(question, null);
            }
        }
    }

    private List<Guid> SpeciesOf(ReportQuestion question) =>
    [
        .. sectionAnswers.ProfiledSpecies.Select(species => species.Id)
            .Union(working.Where(value => value.QuestionId == question.Id && value.SpeciesId is not null).Select(value => value.SpeciesId!.Value))
    ];

    private void ClearValues(ReportQuestion question, Guid? speciesId)
    {
        var targets = working
            .Where(value => value.QuestionId == question.Id
                && value.RowId is null
                && value.SpeciesId == speciesId
                && fields.TryGetValue(value.FieldId, out var entry)
                && IsChangeable(entry.Field, isWhatIfScenario))
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            cleared.Add((target.FieldId, target.SpeciesId));
        }

        working.RemoveAll(targets.Contains);
        valuesByField = null;
    }

    private void DeleteRows(ReportQuestion question)
    {
        var rowIds = sectionAnswers.Rows.Where(row => row.QuestionId == question.Id).Select(row => row.Id).ToList();
        var rowsToAdd = rowInserts.Where(row => row.QuestionId == question.Id).ToList();

        if (rowIds.Count == 0 && rowsToAdd.Count == 0)
        {
            return;
        }

        // A row this save was going to add or move is not wanted either: none of the question's rows are.
        deletedRows.UnionWith(rowIds);
        rowInserts.RemoveAll(row => row.QuestionId == question.Id);
        rowUpdates.RemoveAll(row => row.QuestionId == question.Id);
        working.RemoveAll(value => value.RowId is { } rowId && (deletedRows.Contains(rowId) || rowsToAdd.Any(row => row.Id == rowId)));
        valuesByField = null;
    }

    private ProfileSectionChange ToChange(byte[]? lastUpdated)
    {
        // A field the relevance clearing emptied needs no save, and needs a clear only when a value is actually recorded.
        var clears = recorded
            .Where(value => value.RowId is null && cleared.Contains((value.FieldId, value.SpeciesId)))
            .Select(value => ToClearInstruction(value, fields[value.FieldId].Field))
            .Distinct()
            .ToList();

        // An answer in a row goes with the row, so one whose row is not wanted after all is not saved.
        var liveRowIds = sectionAnswers.Rows.Select(row => row.Id).Where(id => !deletedRows.Contains(id)).Concat(rowInserts.Select(row => row.Id)).ToHashSet();
        var savesToApply = saves.Values.Where(save => save.ProfileVersionQuestionRowId is { } rowId
            ? liveRowIds.Contains(rowId)
            : !cleared.Contains((save.ProfileFieldId, save.SpeciesId)));

        return new ProfileSectionChange
        {
            Saves = [.. savesToApply],
            Clears = clears,
            QuestionRowIdsToDelete = [.. deletedRows],
            QuestionRowsToInsert = [.. rowInserts],
            QuestionRowsToUpdate = [.. rowUpdates],
            ExpectedLastUpdated = lastUpdated
        };
    }

    private sealed record FieldEntry(ReportQuestion Question, ReportField Field);
}
