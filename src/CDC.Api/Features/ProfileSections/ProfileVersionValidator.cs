using System.Globalization;
using System.Text;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileSections;

/// <summary>
/// Validates the sections of one profile version, replicating the legacy <c>SectionBase.Validate()</c>: the section's
/// revision-date rules (<c>ProfileVersionSection.ValidateSectionData</c>), then each question's rules
/// (<c>StandardQuestion</c>, <c>RepeatingQuestion</c>/<c>QuestionRow</c> and <c>PerSpeciesQuestion</c>/<c>QuestionSpecies</c>
/// <c>.Validate()</c>) in question order. One instance validates one profile version: it caches reference lists.
/// </summary>
/// <param name="repository">Reads the options of reference lists, to find which selected values are inactive.</param>
/// <param name="allValues">Every recorded field value in the profile version, across all sections, because a field in one section can control the relevance or visibility of a question or field in another.</param>
/// <param name="version">Facts about the version itself: whether it is a "what-if" scenario and whether any of its species is inactive.</param>
/// <param name="completeReviewStatusId">The identifier of the "Complete" review status, or <see cref="Guid.Empty"/> when the database has none.</param>
/// <param name="now">The current local time, which a "cannot be a future date" answer is compared with.</param>
internal sealed class ProfileVersionValidator(
    IProfileReportContentRepository repository,
    IReadOnlyList<ReportFieldValue> allValues,
    ReportVersionContext version,
    Guid completeReviewStatusId,
    DateTime now)
{
    private const string BooleanDataType = "Boolean";
    private const string DecimalDataType = "Decimal";
    private const string DateDataType = "Date";
    private const string ListDataType = "List";
    private const string MultiValueListDataType = "MultiValueList";
    private const string ProfileVersionDataType = "Profile Version Data";

    private const string FutureDateShortName = "Date Of Most Recent Occurrence In GB";
    private const string ProfiledSpeciesShortName = "Profiled Species";
    private const string OtherAffectedSpeciesShortName = "Other Affected Species";

    private const string FutureDateText = "cannot be a future date";
    private const string InactiveSpeciesText = "contains inactive species";

    // Each incubation period must not exceed the next one in the sequence Minimum, Typical Lower, Typical Upper, Maximum.
    private static readonly Dictionary<string, (string NextShortName, string Text)> IncubationRules = new(StringComparer.Ordinal)
    {
        ["Incubation Period Minimum"] = ("Incubation Period Typical Lower", "must be less than or equal to Typical incubation period range (lower value)"),
        ["Incubation Period Typical Lower"] = ("Incubation Period Typical Upper", "must be less than or equal to Typical incubation period range (upper value)"),
        ["Incubation Period Typical Upper"] = ("Incubation Period Maximum", "must be less than or equal to Maximum incubation period (in days)")
    };

    private readonly Dictionary<Guid, IReadOnlyList<ReportReferenceItem>> referenceItems = [];
    private readonly ILookup<Guid, ReportFieldValue> valuesByField = allValues.ToLookup(value => value.FieldId);

    private enum ReasonKind
    {
        Mandatory,
        ReferenceInactive,
        Irrelevant,
        Custom,
        SpeciesInactive
    }

    /// <summary>Validates one section.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answers">The section's recorded answers.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>Every validation error: the revision-date errors, then each question's errors in question order. Empty when the section is valid.</returns>
    public async Task<IReadOnlyList<ProfileSectionInvalidReasonDto>> ValidateSectionAsync(
        ReportSection section,
        ReportSectionAnswers answers,
        CancellationToken cancellationToken)
    {
        var reasons = new List<ProfileSectionInvalidReasonDto>();

        AddRevisionReasons(section.SectionNumber, answers.Revision, reasons);

        foreach (var question in section.Questions)
        {
            var scope = new QuestionScope(
                section.SectionNumber,
                question,
                answers.QuestionNames.FirstOrDefault(name => name.Id == question.Id)?.Name ?? string.Empty,
                [.. answers.Values.Where(value => value.QuestionId == question.Id)],
                reasons);

            if (question.IsRepeating)
            {
                await ValidateRepeatingQuestionAsync(scope, answers, cancellationToken);
            }
            else if (question.IsPerSpecies)
            {
                await ValidatePerSpeciesQuestionAsync(scope, answers.ProfiledSpecies, cancellationToken);
            }
            else
            {
                await ValidateStandardQuestionAsync(scope, cancellationToken);
            }
        }

        return reasons;
    }

    /// <summary>
    /// The three errors the legacy <c>Profile.GetInvalidReasons()</c> raised for each section, ahead of the section's own
    /// <c>Validate()</c> errors, when a profile was checked for publication: the next policy, VA and author review dates
    /// are mandatory. The "Validate" button on a single section never raised them.
    /// </summary>
    /// <param name="sectionNumber">The number of the section.</param>
    /// <param name="revision">The section's recorded review data, or <see langword="null"/> when the profile version has no record of the section.</param>
    /// <returns>An error for each next review date that is not set; a date is not set when it is missing or the minimum date.</returns>
    internal static IEnumerable<ProfileSectionInvalidReasonDto> NextReviewReasons(int sectionNumber, ReportSectionRevision? revision)
    {
        (DateTime? Date, string FieldName)[] checks =
        [
            (revision?.NextPolicyReview, "Revision dates, Next policy review"),
            (revision?.NextVaReview, "Revision dates, Next VA review"),
            (revision?.NextAuthorReview, "Revision dates, Next author review")
        ];

        foreach (var (date, fieldName) in checks)
        {
            if (date is null || date == DateTime.MinValue)
            {
                yield return new ProfileSectionInvalidReasonDto
                {
                    SectionNumber = sectionNumber,
                    Description = BuildDescription(string.Empty, 0, string.Empty, fieldName, ReasonText(ReasonKind.Mandatory))
                };
            }
        }
    }

    private static bool Has(Guid? id) => id is { } value && value != Guid.Empty;

    private static bool IsFieldGroup(ReportField field) => field.DataTypeName == ReportField.FieldGroupDataType;

    /// <summary>The legacy <c>FieldValueBase.IsEmpty</c> for each data type: text is empty only when it has no characters, and a date is empty when it is unset or the minimum or maximum date.</summary>
    private static bool IsEmpty(ReportField field, List<ReportFieldValue> values)
    {
        var value = values.Count > 0 ? values[0] : null;

        return field.DataTypeName switch
        {
            BooleanDataType => value?.BooleanValue is null,
            DecimalDataType => value?.DecimalValue is null,
            DateDataType => value?.DateValue is not { } date || date == DateTime.MinValue || date == DateTime.MaxValue,
            ListDataType => !Has(value?.ListValue),
            MultiValueListDataType => !values.Any(candidate => Has(candidate.ListValue)),
            ProfileVersionDataType => false,
            _ => string.IsNullOrEmpty(value?.TextValue)
        };
    }

    private static string ReasonText(ReasonKind kind) => kind switch
    {
        ReasonKind.Mandatory => "is required",
        ReasonKind.ReferenceInactive => "uses inactive reference data entry/entries",
        ReasonKind.Irrelevant => "is not relevant due to a previous selection",
        ReasonKind.SpeciesInactive => "species is inactive",
        _ => string.Empty
    };

    /// <summary>The legacy <c>InvalidReason.Description</c>: <c>[Species][ Row N], "Question - Field" reason</c>.</summary>
    private static string BuildDescription(string speciesName, int rowNumber, string questionName, string fieldName, string reasonText)
    {
        var description = new StringBuilder(speciesName);

        if (rowNumber > 0)
        {
            description.Append(description.Length > 0 ? " Row " : "Row ").Append(rowNumber.ToString(CultureInfo.InvariantCulture));
        }

        if (description.Length > 0)
        {
            description.Append(", ");
        }

        description.Append('"').Append(questionName);

        if (fieldName.Length > 0 && questionName.Length > 0)
        {
            description.Append(" - ");
        }

        description.Append(fieldName).Append("\" ").Append(reasonText);

        return description.ToString().TrimEnd();
    }

    private static void AddReason(QuestionScope scope, string speciesName, int rowNumber, ReportField field, FieldEvaluation evaluation) =>
        scope.Reasons.Add(new ProfileSectionInvalidReasonDto
        {
            QuestionId = scope.Question.Id,
            SectionNumber = scope.SectionNumber,
            QuestionNumber = scope.Question.QuestionNumber,
            QuestionShortName = scope.Question.ShortName,
            Description = BuildDescription(speciesName, rowNumber, scope.QuestionName, field.Name, evaluation.Text)
        });

    /// <summary>A minimum, typical or maximum incubation period must not be larger than the next period in the sequence; it is only checked when both periods are answered.</summary>
    private static FieldEvaluation? CheckIncubationPeriod(
        ReportField field,
        List<ReportFieldValue> values,
        IReadOnlyList<ReportField> siblings,
        List<ReportFieldValue> speciesValues)
    {
        if (!IncubationRules.TryGetValue(field.ShortName, out var rule))
        {
            return null;
        }

        var next = siblings.FirstOrDefault(sibling => sibling.ShortName == rule.NextShortName);

        if (next is null)
        {
            return null;
        }

        var nextValue = speciesValues.FirstOrDefault(value => value.FieldId == next.Id)?.DecimalValue;

        return values[0].DecimalValue > nextValue ? FieldEvaluation.Custom(rule.Text) : null;
    }

    /// <summary>The legacy <c>ProfileVersionSection.ValidateSectionData</c>: each review frequency and status must be set, and each review must be complete.</summary>
    private void AddRevisionReasons(int sectionNumber, ReportSectionRevision? revision, List<ProfileSectionInvalidReasonDto> reasons)
    {
        bool IsComplete(Guid? statusId) => Has(statusId) && statusId == completeReviewStatusId;

        (bool IsInvalid, string FieldName, ReasonKind Kind)[] checks =
        [
            (!Has(revision?.PolicyReviewFrequencyId), "Revision dates, Policy review frequency", ReasonKind.Mandatory),
            (!Has(revision?.VaReviewFrequencyId), "Revision dates, VA review frequency", ReasonKind.Mandatory),
            (!Has(revision?.AuthorReviewFrequencyId), "Revision dates, Author review frequency", ReasonKind.Mandatory),
            (!Has(revision?.PolicyReviewStatusId), "Revision dates, Policy review status", ReasonKind.Mandatory),
            (!Has(revision?.VaReviewStatusId), "Revision dates, VA review status", ReasonKind.Mandatory),
            (!Has(revision?.AuthorReviewStatusId), "Revision dates, Author review status", ReasonKind.Mandatory),
            (!IsComplete(revision?.VaReviewStatusId), "Revision dates, VA review must be complete", ReasonKind.Custom),
            (!IsComplete(revision?.PolicyReviewStatusId), "Revision dates, Policy review must be complete", ReasonKind.Custom),
            (!IsComplete(revision?.AuthorReviewStatusId), "Revision dates, Author review must be complete", ReasonKind.Custom)
        ];

        foreach (var (isInvalid, fieldName, kind) in checks)
        {
            if (isInvalid)
            {
                reasons.Add(new ProfileSectionInvalidReasonDto
                {
                    SectionNumber = sectionNumber,
                    Description = BuildDescription(string.Empty, 0, string.Empty, fieldName, ReasonText(kind))
                });
            }
        }
    }

    private async Task ValidateStandardQuestionAsync(QuestionScope scope, CancellationToken cancellationToken)
    {
        var isRelevant = QuestionRelevance.IsRelevant(scope.Question, null, valuesByField);
        var questionValues = scope.Values.Where(value => value.FieldGroupId == Guid.Empty && value.SpeciesId is null && value.RowId is null).ToList();

        foreach (var field in scope.Question.Fields.Where(field => !IsFieldGroup(field)))
        {
            var fieldValues = questionValues.Where(value => value.FieldId == field.Id).ToList();
            var evaluation = await EvaluateStandardFieldAsync(isRelevant, field, fieldValues, cancellationToken);

            if (evaluation is not null)
            {
                AddReason(scope, string.Empty, 0, field, evaluation);
            }
        }
    }

    private async Task ValidateRepeatingQuestionAsync(QuestionScope scope, ReportSectionAnswers answers, CancellationToken cancellationToken)
    {
        var isRelevant = QuestionRelevance.IsRelevant(scope.Question, null, valuesByField);

        foreach (var row in answers.Rows.Where(row => row.QuestionId == scope.Question.Id).OrderBy(row => row.SequenceNumber))
        {
            // The legacy page numbers rows from 1.
            var rowNumber = row.SequenceNumber + 1;
            var rowValues = scope.Values.Where(value => value.RowId == row.Id).ToList();

            foreach (var field in scope.Question.Fields)
            {
                if (IsFieldGroup(field))
                {
                    await ValidateFieldGroupAsync(scope, field, answers.ProfiledSpecies, rowNumber, rowValues, cancellationToken);
                    continue;
                }

                var fieldValues = rowValues.Where(value => value.FieldId == field.Id && value.FieldGroupId == Guid.Empty).ToList();
                var evaluation = await EvaluateScalarFieldAsync(isRelevant, field, fieldValues, cancellationToken);

                if (evaluation is not null)
                {
                    AddReason(scope, string.Empty, rowNumber, field, evaluation);
                }
            }
        }
    }

    /// <summary>A field group holds one set of child fields per profiled species; each child is validated like a per-species field.</summary>
    private async Task ValidateFieldGroupAsync(
        QuestionScope scope,
        ReportField group,
        IReadOnlyList<ReportSpecies> species,
        int rowNumber,
        List<ReportFieldValue> rowValues,
        CancellationToken cancellationToken)
    {
        foreach (var currentSpecies in species)
        {
            var childValues = rowValues
                .Where(value => value.FieldGroupId == group.Id && value.SpeciesId == currentSpecies.Id)
                .ToList();

            // The legacy species inside a field group never had their relevance recalculated, so they stay relevant.
            await ValidateSpeciesFieldsAsync(scope, currentSpecies, true, group.Children, childValues, rowNumber, cancellationToken);
        }
    }

    private async Task ValidatePerSpeciesQuestionAsync(QuestionScope scope, IReadOnlyList<ReportSpecies> species, CancellationToken cancellationToken)
    {
        var directValues = scope.Values.Where(value => value.FieldGroupId == Guid.Empty).ToList();

        foreach (var currentSpecies in species)
        {
            var isRelevant = QuestionRelevance.IsRelevant(scope.Question, currentSpecies.Id, valuesByField);
            var speciesValues = directValues.Where(value => value.SpeciesId == currentSpecies.Id).ToList();

            await ValidateSpeciesFieldsAsync(scope, currentSpecies, isRelevant, scope.Question.Fields, speciesValues, 0, cancellationToken);
        }
    }

    private async Task ValidateSpeciesFieldsAsync(
        QuestionScope scope,
        ReportSpecies species,
        bool isRelevant,
        IReadOnlyList<ReportField> fields,
        List<ReportFieldValue> speciesValues,
        int rowNumber,
        CancellationToken cancellationToken)
    {
        foreach (var field in fields.Where(field => !IsFieldGroup(field)))
        {
            var evaluation = await EvaluateSpeciesFieldAsync(species, isRelevant, field, fields, speciesValues, cancellationToken);

            if (evaluation is not null)
            {
                AddReason(scope, species.Name, rowNumber, field, evaluation);
            }
        }
    }

    /// <summary>The legacy <c>StandardQuestion.ValidateFieldValue</c>: the rules shared with repeating rows, plus two checks that single out fields by name.</summary>
    private async Task<FieldEvaluation?> EvaluateStandardFieldAsync(
        bool questionIsRelevant,
        ReportField field,
        List<ReportFieldValue> values,
        CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateScalarFieldAsync(questionIsRelevant, field, values, cancellationToken);

        if (!questionIsRelevant)
        {
            return evaluation;
        }

        // Question 2.3: the date of the most recent occurrence in GB cannot be in the future.
        if (field.ShortName == FutureDateShortName
            && field.DataTypeName == DateDataType
            && !IsEmpty(field, values)
            && values[0].DateValue > now)
        {
            evaluation = FieldEvaluation.Custom(FutureDateText);
        }

        // Questions 1.3 and 1.4: the profiled and other affected species lists cannot include an inactive species.
        if (ContainsInactiveSpecies(field))
        {
            evaluation = FieldEvaluation.Custom(InactiveSpeciesText);
        }

        return evaluation;
    }

    /// <summary>The legacy <c>StandardQuestion</c>/<c>QuestionRow.ValidateFieldValue</c> rules every field outside a per-species scope follows. A hidden field is never mandatory or irrelevant.</summary>
    private async Task<FieldEvaluation?> EvaluateScalarFieldAsync(
        bool questionIsRelevant,
        ReportField field,
        List<ReportFieldValue> values,
        CancellationToken cancellationToken)
    {
        var isEmpty = IsEmpty(field, values);

        if (!questionIsRelevant)
        {
            return !isEmpty && !field.AffectsRelevance && !IsReadOnlyByDefault(field) && IsVisible(field)
                ? FieldEvaluation.Of(ReasonKind.Irrelevant)
                : null;
        }

        FieldEvaluation? evaluation = null;

        if (IsVisible(field) && field.IsMandatory && isEmpty)
        {
            evaluation = FieldEvaluation.Of(ReasonKind.Mandatory);
        }

        if (!isEmpty && await UsesInactiveReferenceAsync(field, values, cancellationToken))
        {
            evaluation = FieldEvaluation.Of(ReasonKind.ReferenceInactive);
        }

        return evaluation;
    }

    /// <summary>The legacy <c>QuestionSpecies.ValidateFieldValue</c>: an inactive species fails every field; otherwise the same rules, with no visibility check and the incubation period ordering.</summary>
    private async Task<FieldEvaluation?> EvaluateSpeciesFieldAsync(
        ReportSpecies species,
        bool isRelevant,
        ReportField field,
        IReadOnlyList<ReportField> siblings,
        List<ReportFieldValue> speciesValues,
        CancellationToken cancellationToken)
    {
        if (!species.IsActive)
        {
            return FieldEvaluation.Of(ReasonKind.SpeciesInactive);
        }

        var values = speciesValues.Where(value => value.FieldId == field.Id).ToList();
        var isEmpty = IsEmpty(field, values);

        if (!isRelevant)
        {
            return !isEmpty && !field.AffectsRelevance && !IsReadOnlyByDefault(field)
                ? FieldEvaluation.Of(ReasonKind.Irrelevant)
                : null;
        }

        FieldEvaluation? evaluation = null;

        if (field.IsMandatory && isEmpty)
        {
            evaluation = FieldEvaluation.Of(ReasonKind.Mandatory);
        }

        if (!isEmpty && await UsesInactiveReferenceAsync(field, values, cancellationToken))
        {
            evaluation = FieldEvaluation.Of(ReasonKind.ReferenceInactive);
        }

        var incubation = isEmpty ? null : CheckIncubationPeriod(field, values, siblings, speciesValues);

        return incubation ?? evaluation;
    }

    /// <summary>The legacy "inactive reference data" check: a selected value of a maintainable list that has been made inactive.</summary>
    private async Task<bool> UsesInactiveReferenceAsync(ReportField field, List<ReportFieldValue> values, CancellationToken cancellationToken)
    {
        if (!field.ReferenceTableIsMaintainable || field.DataTypeName is not (ListDataType or MultiValueListDataType))
        {
            return false;
        }

        var selected = values.Where(value => Has(value.ListValue)).Select(value => value.ListValue!.Value).ToHashSet();

        if (selected.Count == 0)
        {
            return false;
        }

        if (!referenceItems.TryGetValue(field.ReferenceTableId, out var items))
        {
            items = await repository.GetReferenceItemsAsync(field.ReferenceTableId, true, cancellationToken);
            referenceItems[field.ReferenceTableId] = items;
        }

        return items.Any(item => !item.IsActive && selected.Contains(item.Id));
    }

    /// <summary>The legacy checked whether the field's text contained "(inactive)", which for these two fields lists the version's species and marks each inactive one.</summary>
    private bool ContainsInactiveSpecies(ReportField field) => field.ShortName switch
    {
        ProfiledSpeciesShortName => version.HasInactiveProfiledSpecies,
        OtherAffectedSpeciesShortName => version.HasInactiveOtherAffectedSpecies,
        _ => false
    };

    /// <summary>Legacy "read-only by default" depends on whether the profile is the current situation or a "what-if" scenario.</summary>
    private bool IsReadOnlyByDefault(ReportField field) =>
        version.IsWhatIfScenario ? field.IsReadOnlyScenario : field.IsReadOnlyCurrentSituation;

    /// <summary>The legacy <c>SectionBase.CalculateVisibility</c>: a field with a controlling list field is visible only while that field has the answer the field names.</summary>
    private bool IsVisible(ReportField field)
    {
        if (field.RelevanceFieldId == Guid.Empty)
        {
            return true;
        }

        var controlling = valuesByField[field.RelevanceFieldId].ToList();
        var answer = controlling.FirstOrDefault(value => value.SpeciesId is null && value.RowId is null) ?? controlling.FirstOrDefault();

        return (answer?.ListValue ?? Guid.Empty) == field.RelevanceValueId;
    }

    private sealed record QuestionScope(
        int SectionNumber,
        ReportQuestion Question,
        string QuestionName,
        List<ReportFieldValue> Values,
        List<ProfileSectionInvalidReasonDto> Reasons);

    private sealed record FieldEvaluation(ReasonKind Kind, string Text)
    {
        public static FieldEvaluation Of(ReasonKind kind) => new(kind, ReasonText(kind));

        public static FieldEvaluation Custom(string text) => new(ReasonKind.Custom, text);
    }
}
