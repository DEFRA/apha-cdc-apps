using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Models;
using Ganss.Xss;

namespace CDC.Web.Infrastructure;

/// <summary>Builds the differences-only report for two profile versions.</summary>
public interface IProfileVersionComparisonService
{
    /// <param name="left">The earlier version.</param>
    /// <param name="right">The later version.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<ProfileVersionComparisonReport> BuildReportAsync(
        ProfileVersionOption left,
        ProfileVersionOption right,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compares each question's recorded answers across all profile sections, keeping only those that
/// differ, as the legacy <c>ProfileVersionComparisonReportPdf</c> did.
/// </summary>
public sealed class ProfileVersionComparisonService(IProfileSectionsApiService sectionsApi) : IProfileVersionComparisonService
{
    private const string NotAnswered = "Not answered";
    private const string FieldGroup = "Field Group";

    /// <inheritdoc />
    public async Task<ProfileVersionComparisonReport> BuildReportAsync(
        ProfileVersionOption left,
        ProfileVersionOption right,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var metadata = await sectionsApi.GetProfileQuestionnaireMetadataAsync(cancellationToken);
        var referenceValues = new Dictionary<Guid, IReadOnlyDictionary<Guid, string>>();
        var sections = new List<ComparisonSection>();

        foreach (var section in metadata.Sections.OrderBy(item => item.SectionNumber))
        {
            var leftTask = sectionsApi.GetProfileSectionAnswersAsync(left.VersionId, section.Id, cancellationToken);
            var rightTask = sectionsApi.GetProfileSectionAnswersAsync(right.VersionId, section.Id, cancellationToken);
            await Task.WhenAll(leftTask, rightTask);

            var questions = await CompareSectionAsync(section, leftTask.Result, rightTask.Result, referenceValues, cancellationToken);
            sections.Add(new ComparisonSection($"{section.SectionNumber}. {section.Name}", questions));
        }

        return new ProfileVersionComparisonReport
        {
            LeftTitle = left.FullLabel,
            RightTitle = right.FullLabel,
            Sections = sections
        };
    }

    private async Task<IReadOnlyList<ComparisonQuestion>> CompareSectionAsync(
        ProfileSectionMetadataDto section,
        ProfileSectionAnswersDto leftAnswers,
        ProfileSectionAnswersDto rightAnswers,
        Dictionary<Guid, IReadOnlyDictionary<Guid, string>> referenceValues,
        CancellationToken cancellationToken)
    {
        var names = leftAnswers.QuestionNames.Concat(rightAnswers.QuestionNames)
            .GroupBy(name => name.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var leftValues = leftAnswers.FieldValues.ToLookup(value => value.QuestionId);
        var rightValues = rightAnswers.FieldValues.ToLookup(value => value.QuestionId);
        var questions = new List<ComparisonQuestion>();

        foreach (var question in section.Questions.OrderBy(item => item.QuestionNumber))
        {
            var fields = new List<ComparisonField>();

            foreach (var field in question.Fields
                .Where(item => !string.Equals(item.DataTypeName, FieldGroup, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.FieldNumber))
            {
                var options = await GetOptionsAsync(field, referenceValues, cancellationToken);
                var isHtml = string.Equals(field.DataTypeName, "Long Text", StringComparison.OrdinalIgnoreCase);
                var leftText = Format(field, leftValues[question.Id], options, isHtml);
                var rightText = Format(field, rightValues[question.Id], options, isHtml);

                if (!string.Equals(leftText, rightText, StringComparison.Ordinal))
                {
                    fields.Add(new ComparisonField(field.Name, leftText, rightText, isHtml));
                }
            }

            if (fields.Count > 0)
            {
                var text = names.TryGetValue(question.Id, out var name) ? name : question.ShortName;
                questions.Add(new ComparisonQuestion($"{section.SectionNumber}.{question.QuestionNumber}", text, fields));
            }
        }

        return questions;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetOptionsAsync(
        ProfileFieldMetadataDto field,
        Dictionary<Guid, IReadOnlyDictionary<Guid, string>> cache,
        CancellationToken cancellationToken)
    {
        if (field.ReferenceTableId == Guid.Empty)
        {
            return new Dictionary<Guid, string>();
        }

        if (!cache.TryGetValue(field.ReferenceTableId, out var options))
        {
            var values = await sectionsApi.GetReferenceValuesAsync(field.ReferenceTableId, cancellationToken);
            options = values.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First().Value);
            cache[field.ReferenceTableId] = options;
        }

        return options;
    }

    private static string Format(
        ProfileFieldMetadataDto field,
        IEnumerable<ProfileFieldValueDto> allValues,
        IReadOnlyDictionary<Guid, string> options,
        bool isHtml)
    {
        var rendered = allValues
            .Where(value => value.FieldNumber == field.FieldNumber)
            .Select(value => FormatSingle(value, options, isHtml))
            .Where(text => text.Length > 0)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return rendered.Count == 0 ? NotAnswered : string.Join(", ", rendered);
    }

    private static string FormatSingle(ProfileFieldValueDto value, IReadOnlyDictionary<Guid, string> options, bool isHtml)
    {
        if (value.BooleanValue is { } boolean)
        {
            return boolean ? "Yes" : "No";
        }

        if (value.ListValue is { } list)
        {
            return options.TryGetValue(list, out var option) ? option : list.ToString();
        }

        if (value.DecimalValue is { } number)
        {
            return number.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (value.DateValue is { } date)
        {
            return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrWhiteSpace(value.TextValue))
        {
            return string.Empty;
        }

        return isHtml ? new HtmlSanitizer().Sanitize(value.TextValue).Trim() : value.TextValue.Trim();
    }
}
