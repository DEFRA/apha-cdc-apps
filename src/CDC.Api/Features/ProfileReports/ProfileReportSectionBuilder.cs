using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileReports;

/// <summary>
/// Turns the raw questionnaire and recorded answers of one profile version into display-ready
/// report content, replicating how the legacy <c>AnswerTable</c>/<c>FieldValueCell</c>/
/// <c>ListDataParagraph</c> classes laid out standard, repeating and per-species questions, list
/// options, scalar values and relevance. One instance builds one report: it caches reference lists.
/// </summary>
/// <param name="repository">Reads reference and lookup lists.</param>
/// <param name="allValues">Every recorded field value in the profile version, across all sections, because a field in one section can control the relevance of a question in another.</param>
internal sealed class ProfileReportSectionBuilder(IProfileReportContentRepository repository, IReadOnlyList<ReportFieldValue> allValues)
{
    private const string BooleanDataType = "Boolean";
    private const string DecimalDataType = "Decimal";
    private const string DateDataType = "Date";
    private const string ListDataType = "List";
    private const string MultiValueListDataType = "MultiValueList";
    private const string InactiveSuffix = " (inactive)";

    private readonly Dictionary<Guid, IReadOnlyList<ReportReferenceItem>> referenceItems = [];
    private readonly ILookup<Guid, ReportFieldValue> valuesByField = allValues.ToLookup(value => value.FieldId);

    /// <summary>Builds one section's report content.</summary>
    /// <param name="section">The section's questionnaire definition.</param>
    /// <param name="answers">The section's recorded answers.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The section, ready to print.</returns>
    public async Task<ProfileReportSectionDto> BuildAsync(ReportSection section, ReportSectionAnswers answers, CancellationToken cancellationToken)
    {
        var questions = new List<ProfileReportQuestionDto>();

        foreach (var question in section.Questions)
        {
            questions.Add(await BuildQuestionAsync(question, answers, cancellationToken));
        }

        return new ProfileReportSectionDto
        {
            Id = section.Id,
            SectionNumber = section.SectionNumber,
            Name = section.Name,
            ShortName = section.ShortName,
            Note = section.Note,
            Questions = questions
        };
    }

    private static ProfileReportQuestionKind GetKind(ReportQuestion question)
    {
        if (question.IsPerSpecies)
        {
            return ProfileReportQuestionKind.PerSpecies;
        }

        return question.IsRepeating ? ProfileReportQuestionKind.Repeating : ProfileReportQuestionKind.Standard;
    }

    private static string FormatDecimal(decimal? value) =>
        value?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatDate(DateTime? value) =>
        value?.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private async Task<ProfileReportQuestionDto> BuildQuestionAsync(ReportQuestion question, ReportSectionAnswers answers, CancellationToken cancellationToken)
    {
        var name = answers.QuestionNames.FirstOrDefault(candidate => candidate.Id == question.Id);
        var kind = GetKind(question);
        var values = answers.Values.Where(value => value.QuestionId == question.Id).ToList();
        var questionName = name?.Name ?? string.Empty;

        var questionIsRelevant = kind == ProfileReportQuestionKind.PerSpecies || IsRelevant(question, null);
        var groups = questionIsRelevant
            ? await BuildGroupsAsync(question, kind, questionName, answers, values, cancellationToken)
            : [];

        return new ProfileReportQuestionDto
        {
            Id = question.Id,
            QuestionNumber = question.QuestionNumber,
            Name = questionName,
            NonTechnicalName = name?.NonTechnicalName ?? string.Empty,
            Kind = kind,
            IsRelevant = questionIsRelevant,
            Groups = groups
        };
    }

    private async Task<IReadOnlyList<ProfileReportAnswerGroupDto>> BuildGroupsAsync(
        ReportQuestion question,
        ProfileReportQuestionKind kind,
        string questionName,
        ReportSectionAnswers answers,
        List<ReportFieldValue> values,
        CancellationToken cancellationToken)
    {
        var direct = values.Where(value => value.FieldGroupId == Guid.Empty).ToList();

        if (kind == ProfileReportQuestionKind.PerSpecies)
        {
            var speciesGroups = new List<ProfileReportAnswerGroupDto>();

            foreach (var species in answers.ProfiledSpecies)
            {
                var isRelevant = IsRelevant(question, species.Id);
                var fields = isRelevant
                    ? await BuildFieldsAsync(question.Fields, direct.Where(value => value.SpeciesId == species.Id).ToList(), values, answers, null, cancellationToken)
                    : [];
                speciesGroups.Add(new ProfileReportAnswerGroupDto { Heading = species.Name, IsRelevant = isRelevant, Fields = fields });
            }

            return speciesGroups;
        }

        if (kind == ProfileReportQuestionKind.Repeating)
        {
            var rowGroups = new List<ProfileReportAnswerGroupDto>();

            foreach (var row in answers.Rows.Where(candidate => candidate.QuestionId == question.Id).OrderBy(candidate => candidate.SequenceNumber))
            {
                var fields = await BuildFieldsAsync(question.Fields, direct.Where(value => value.RowId == row.Id).ToList(), values, answers, row.Id, cancellationToken);
                rowGroups.Add(new ProfileReportAnswerGroupDto { Heading = $"{questionName} {row.SequenceNumber + 1}", Fields = fields });
            }

            return rowGroups;
        }

        var standardFields = await BuildFieldsAsync(question.Fields, direct.Where(value => value.SpeciesId is null && value.RowId is null).ToList(), values, answers, null, cancellationToken);

        return [new ProfileReportAnswerGroupDto { Fields = standardFields }];
    }

    private async Task<IReadOnlyList<ProfileReportFieldDto>> BuildFieldsAsync(
        IReadOnlyList<ReportField> fields,
        List<ReportFieldValue> scopeValues,
        List<ReportFieldValue> questionValues,
        ReportSectionAnswers answers,
        Guid? rowId,
        CancellationToken cancellationToken)
    {
        var result = new List<ProfileReportFieldDto>();

        foreach (var field in fields.OrderBy(candidate => candidate.FieldNumber))
        {
            if (field.DataTypeName == ReportField.FieldGroupDataType)
            {
                result.Add(await BuildFieldGroupAsync(field, questionValues, answers, rowId, cancellationToken));
            }
            else
            {
                result.Add(await BuildFieldAsync(field, scopeValues.Where(value => value.FieldId == field.Id).ToList(), cancellationToken));
            }
        }

        return result;
    }

    private async Task<ProfileReportFieldDto> BuildFieldAsync(ReportField field, List<ReportFieldValue> fieldValues, CancellationToken cancellationToken)
    {
        switch (field.DataTypeName)
        {
            case BooleanDataType:
                var answer = fieldValues.FirstOrDefault()?.BooleanValue;

                return new ProfileReportFieldDto
                {
                    Label = field.Name,
                    Kind = ProfileReportFieldKind.Options,
                    Options = [new ProfileReportOptionDto("Yes", answer == true), new ProfileReportOptionDto("No", answer == false)]
                };
            case ListDataType:
            case MultiValueListDataType:
                return new ProfileReportFieldDto
                {
                    Label = field.Name,
                    Kind = ProfileReportFieldKind.Options,
                    Options = await BuildListOptionsAsync(field, fieldValues, cancellationToken)
                };
            default:
                return new ProfileReportFieldDto { Label = field.Name, Kind = ProfileReportFieldKind.Text, Text = FormatText(field, fieldValues.FirstOrDefault()) };
        }
    }

    private async Task<ProfileReportFieldDto> BuildFieldGroupAsync(ReportField field, List<ReportFieldValue> questionValues, ReportSectionAnswers answers, Guid? rowId, CancellationToken cancellationToken)
    {
        var groups = new List<ProfileReportAnswerGroupDto>();

        foreach (var species in answers.ProfiledSpecies)
        {
            var childValues = questionValues
                .Where(value => value.FieldGroupId == field.Id && value.SpeciesId == species.Id && (rowId is null || value.RowId == rowId))
                .ToList();

            var fields = await BuildFieldsAsync(field.Children, childValues, questionValues, answers, rowId, cancellationToken);
            groups.Add(new ProfileReportAnswerGroupDto { Heading = species.Name, Fields = fields });
        }

        return new ProfileReportFieldDto { Label = field.Name, Kind = ProfileReportFieldKind.FieldGroup, Groups = groups };
    }

    private async Task<IReadOnlyList<ProfileReportOptionDto>> BuildListOptionsAsync(ReportField field, List<ReportFieldValue> fieldValues, CancellationToken cancellationToken)
    {
        var items = await GetReferenceItemsAsync(field, cancellationToken);
        var options = new List<ProfileReportOptionDto>();

        foreach (var item in items)
        {
            var selected = fieldValues.Any(value => value.ListValue == item.Id);

            if (item.IsActive || selected)
            {
                options.Add(new ProfileReportOptionDto(item.IsActive ? item.Text : item.Text + InactiveSuffix, selected));
            }
        }

        return options;
    }

    private static string FormatText(ReportField field, ReportFieldValue? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return field.DataTypeName switch
        {
            DecimalDataType => FormatDecimal(value.DecimalValue),
            DateDataType => FormatDate(value.DateValue),
            _ => value.TextValue ?? string.Empty
        };
    }

    private async Task<IReadOnlyList<ReportReferenceItem>> GetReferenceItemsAsync(ReportField field, CancellationToken cancellationToken)
    {
        if (!referenceItems.TryGetValue(field.ReferenceTableId, out var items))
        {
            items = await repository.GetReferenceItemsAsync(field.ReferenceTableId, field.ReferenceTableIsMaintainable, cancellationToken);
            referenceItems[field.ReferenceTableId] = items;
        }

        return items;
    }

    private bool IsRelevant(ReportQuestion question, Guid? speciesId) => QuestionRelevance.IsRelevant(question, speciesId, valuesByField);
}
