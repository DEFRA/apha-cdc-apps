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
    private readonly List<ReportFieldValue> recorded;
    private readonly List<ReportFieldValue> working;
    private readonly Dictionary<Guid, ProfileFieldSaveInstruction> saves = [];
    private readonly HashSet<(Guid FieldId, Guid? SpeciesId)> cleared = [];
    private readonly HashSet<Guid> deletedRows = [];
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

        // Only standard (non-per-species, non-repeating) questions are edited through the page. A field group is a
        // container for a repeating question's nested fields, not an answerable field.
        editableFields = section.Questions
            .Where(question => !question.IsPerSpecies && !question.IsRepeating)
            .SelectMany(question => question.Fields.Select(field => new FieldEntry(question, field)))
            .Where(entry => IsChangeable(entry.Field, isWhatIfScenario))
            .ToDictionary(entry => entry.Field.Id);

        // A question's relevance can hang on an answer in another section, so every section's answers are considered.
        recorded = [.. answersBySection.Values.SelectMany(answers => answers.Values)];
        working = [.. recorded];
    }

    /// <summary>Plans a save: the posted answers that differ from what is recorded, then the clearing of what the answers have made irrelevant.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answersBySection">Every section's recorded answers, by section.</param>
    /// <param name="isWhatIfScenario">Whether the profile is a "what-if" scenario, which has its own read-only fields.</param>
    /// <param name="posted">The answers submitted for the section.</param>
    /// <param name="lastUpdated">The row version of the section the caller last read, or <see langword="null"/>.</param>
    /// <returns>The change to apply; it has no value changes when the answers equal what is recorded and nothing is irrelevant.</returns>
    public static ProfileSectionChange PlanSave(
        ReportSection section,
        IReadOnlyDictionary<Guid, ReportSectionAnswers> answersBySection,
        bool isWhatIfScenario,
        IReadOnlyList<ProfileFieldAnswerInputDto> posted,
        byte[]? lastUpdated)
    {
        var planner = new ProfileSectionChangePlanner(section, answersBySection, isWhatIfScenario);

        planner.ApplyPostedAnswers(posted);
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

    private static ProfileFieldSaveInstruction ToSaveInstruction(ReportField field, ProfileFieldAnswerInputDto answer) => new()
    {
        ProfileFieldId = field.Id,
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
            return save.MultiListValues.Select(listValue => new ReportFieldValue { QuestionId = entry.Question.Id, FieldId = entry.Field.Id, ListValue = listValue });
        }

        var hasValue = save.BooleanValue is not null || save.ListValue is not null || save.DecimalValue is not null || save.DateValue is not null || save.TextValue is not null;

        return hasValue
            ? [new ReportFieldValue
            {
                QuestionId = entry.Question.Id,
                FieldId = entry.Field.Id,
                BooleanValue = save.BooleanValue,
                ListValue = save.ListValue,
                DecimalValue = save.DecimalValue,
                DateValue = save.DateValue,
                TextValue = save.TextValue
            }]
            : [];
    }

    private static bool IsDirectValue(ReportFieldValue value, Guid fieldId) =>
        value.FieldId == fieldId && value.SpeciesId is null && value.RowId is null;

    private ILookup<Guid, ReportFieldValue> ValuesByField => valuesByField ??= working.ToLookup(value => value.FieldId);

    private void ApplyPostedAnswers(IReadOnlyList<ProfileFieldAnswerInputDto> posted)
    {
        foreach (var answer in posted)
        {
            if (!editableFields.TryGetValue(answer.ProfileFieldId, out var entry))
            {
                continue;
            }

            var existing = working.Where(value => IsDirectValue(value, entry.Field.Id)).ToList();

            if (!Differs(entry.Field, existing, answer))
            {
                continue;
            }

            var save = ToSaveInstruction(entry.Field, answer);
            saves[entry.Field.Id] = save;
            working.RemoveAll(value => IsDirectValue(value, entry.Field.Id));
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

        if (rowIds.Count == 0)
        {
            return;
        }

        deletedRows.UnionWith(rowIds);
        working.RemoveAll(value => value.RowId is { } rowId && deletedRows.Contains(rowId));
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

        return new ProfileSectionChange
        {
            Saves = [.. saves.Values.Where(save => !cleared.Contains((save.ProfileFieldId, null)))],
            Clears = clears,
            QuestionRowIdsToDelete = [.. deletedRows],
            ExpectedLastUpdated = lastUpdated
        };
    }

    private sealed record FieldEntry(ReportQuestion Question, ReportField Field);
}
