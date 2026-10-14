using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Models;

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
/// differ. Per-species answers are compared species by species and repeating answers row by row
/// (by position), as the legacy <c>ProfileVersionComparisonReportPdf</c> did.
/// </summary>
public sealed partial class ProfileVersionComparisonService(
    IProfileSectionsApiService sectionsApi,
    ISpeciesApiService speciesApi) : IProfileVersionComparisonService
{
    private const string NotAnswered = "Not answered";
    private const string FieldGroup = "Field Group";

    private readonly record struct PartKey(int? Row, Guid? Species);

    /// <inheritdoc />
    public async Task<ProfileVersionComparisonReport> BuildReportAsync(
        ProfileVersionOption left,
        ProfileVersionOption right,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var metadata = await sectionsApi.GetProfileQuestionnaireMetadataAsync(cancellationToken);
        var lookups = new Lookups(sectionsApi, speciesApi, cancellationToken);
        var sections = new List<ComparisonSection>();

        foreach (var section in metadata.Sections.OrderBy(item => item.SectionNumber))
        {
            var leftTask = sectionsApi.GetProfileSectionAnswersAsync(left.VersionId, section.Id, cancellationToken);
            var rightTask = sectionsApi.GetProfileSectionAnswersAsync(right.VersionId, section.Id, cancellationToken);
            await Task.WhenAll(leftTask, rightTask);

            var questions = await CompareSectionAsync(section, leftTask.Result, rightTask.Result, lookups);
            sections.Add(new ComparisonSection($"{section.SectionNumber}. {section.Name}", questions));
        }

        return new ProfileVersionComparisonReport
        {
            LeftTitle = left.FullLabel,
            RightTitle = right.FullLabel,
            Sections = sections
        };
    }

    private static async Task<IReadOnlyList<ComparisonQuestion>> CompareSectionAsync(
        ProfileSectionMetadataDto section,
        ProfileSectionAnswersDto leftAnswers,
        ProfileSectionAnswersDto rightAnswers,
        Lookups lookups)
    {
        var names = leftAnswers.QuestionNames.Concat(rightAnswers.QuestionNames)
            .GroupBy(name => name.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var questions = new List<ComparisonQuestion>();

        foreach (var question in section.Questions.OrderBy(item => item.QuestionNumber))
        {
            var text = names.TryGetValue(question.Id, out var name) ? name : question.ShortName;
            var groups = await CompareQuestionAsync(question, text, leftAnswers, rightAnswers, lookups);

            if (groups.Count > 0)
            {
                questions.Add(new ComparisonQuestion($"{section.SectionNumber}.{question.QuestionNumber}", text, groups));
            }
        }

        return questions;
    }

    private static async Task<IReadOnlyList<ComparisonGroup>> CompareQuestionAsync(
        ProfileQuestionMetadataDto question,
        string questionText,
        ProfileSectionAnswersDto leftAnswers,
        ProfileSectionAnswersDto rightAnswers,
        Lookups lookups)
    {
        var leftValues = Keyed(question, leftAnswers);
        var rightValues = Keyed(question, rightAnswers);
        var keys = leftValues.Select(item => item.Key).Concat(rightValues.Select(item => item.Key)).Distinct().ToList();
        var ordered = new List<(PartKey Key, string? Species)>();

        foreach (var key in keys)
        {
            ordered.Add((key, key.Species is { } species ? await lookups.SpeciesNameAsync(species) : null));
        }

        var fields = question.Fields
            .Where(item => !string.Equals(item.DataTypeName, FieldGroup, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.FieldNumber)
            .ToList();
        var groups = new List<ComparisonGroup>();

        foreach (var (key, species) in ordered.OrderBy(item => item.Key.Row ?? -1).ThenBy(item => item.Species, StringComparer.OrdinalIgnoreCase))
        {
            var differing = new List<ComparisonField>();

            foreach (var field in fields)
            {
                var options = await lookups.OptionsAsync(field);
                var isHtml = string.Equals(field.DataTypeName, "Long Text", StringComparison.OrdinalIgnoreCase);
                var leftText = Format(field, leftValues.Where(item => item.Key == key).Select(item => item.Value), options, isHtml);
                var rightText = Format(field, rightValues.Where(item => item.Key == key).Select(item => item.Value), options, isHtml);

                if (!string.Equals(leftText, rightText, StringComparison.Ordinal))
                {
                    var (leftSegments, rightSegments) = Present(leftText, rightText);
                    differing.Add(new ComparisonField(field.Name, leftSegments, rightSegments));
                }
            }

            if (differing.Count > 0)
            {
                groups.Add(new ComparisonGroup(Heading(questionText, key, species), differing));
            }
        }

        return groups;
    }

    private static List<(PartKey Key, ProfileFieldValueDto Value)> Keyed(ProfileQuestionMetadataDto question, ProfileSectionAnswersDto answers)
    {
        Dictionary<Guid, int> rows = question.IsRepeating
            ? answers.QuestionRows
                .Where(row => row.QuestionId == question.Id)
                .OrderBy(row => row.SequenceNumber)
                .Select((row, index) => (row.Id, index))
                .ToDictionary(item => item.Id, item => item.index)
            : [];

        return
        [
            .. answers.FieldValues
                .Where(value => value.QuestionId == question.Id)
                .Select(value => (
                    new PartKey(
                        value.ProfileVersionQuestionRowId is { } rowId && rows.TryGetValue(rowId, out var index) ? index : null,
                        value.SpeciesId),
                    value))
        ];
    }

    private static string? Heading(string questionText, PartKey key, string? species)
    {
        var parts = new List<string>();

        if (key.Row is { } row)
        {
            parts.Add($"{questionText} {row + 1}");
        }

        if (species is not null)
        {
            parts.Add(species);
        }

        return parts.Count == 0 ? null : string.Join(" - ", parts);
    }

    private static (IReadOnlyList<ComparisonSegment> Left, IReadOnlyList<ComparisonSegment> Right) Present(string left, string right)
    {
        if (left.Length == 0)
        {
            return ([new ComparisonSegment(NotAnswered)], [new ComparisonSegment(right, ComparisonChange.Added)]);
        }

        if (right.Length == 0)
        {
            return ([new ComparisonSegment(left, ComparisonChange.Removed)], [new ComparisonSegment(NotAnswered)]);
        }

        return TextDiff.Compare(left, right);
    }

    private static string Format(
        ProfileFieldMetadataDto field,
        IEnumerable<ProfileFieldValueDto> values,
        IReadOnlyDictionary<Guid, string> options,
        bool isHtml) =>
        string.Join(
            ", ",
            values
                .Where(value => value.FieldNumber == field.FieldNumber)
                .Select(value => FormatSingle(value, options, isHtml))
                .Where(text => text.Length > 0)
                .Order(StringComparer.OrdinalIgnoreCase));

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

        return isHtml ? HtmlToText(value.TextValue) : value.TextValue.Trim();
    }

    // Like the legacy report, rich text is compared as plain text; the output is encoded when rendered.
    private static string HtmlToText(string html)
    {
        var withBreaks = BlockEndPattern().Replace(html, "\n");
        var decoded = WebUtility.HtmlDecode(TagPattern().Replace(withBreaks, string.Empty));

        return ExtraBlankLinesPattern().Replace(decoded, "\n\n").Trim();
    }

    [GeneratedRegex(@"</(p|div|li|tr|h[1-6])\s*>|<br\s*/?>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BlockEndPattern();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"(\s*\n){3,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExtraBlankLinesPattern();

    private sealed class Lookups(IProfileSectionsApiService sectionsApi, ISpeciesApiService speciesApi, CancellationToken cancellationToken)
    {
        private readonly Dictionary<Guid, IReadOnlyDictionary<Guid, string>> referenceValues = [];
        private IReadOnlyDictionary<Guid, string>? speciesNames;

        public async Task<IReadOnlyDictionary<Guid, string>> OptionsAsync(ProfileFieldMetadataDto field)
        {
            if (field.ReferenceTableId == Guid.Empty)
            {
                return new Dictionary<Guid, string>();
            }

            if (!referenceValues.TryGetValue(field.ReferenceTableId, out var options))
            {
                var values = await sectionsApi.GetReferenceValuesAsync(field.ReferenceTableId, cancellationToken);
                options = values.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First().Value);
                referenceValues[field.ReferenceTableId] = options;
            }

            return options;
        }

        public async Task<string> SpeciesNameAsync(Guid speciesId)
        {
            speciesNames ??= (await speciesApi.GetAllSpeciesAsync(cancellationToken))
                .GroupBy(species => species.Id)
                .ToDictionary(group => group.Key, group => group.First().Description);

            return speciesNames.TryGetValue(speciesId, out var name) ? name : speciesId.ToString();
        }
    }
}
