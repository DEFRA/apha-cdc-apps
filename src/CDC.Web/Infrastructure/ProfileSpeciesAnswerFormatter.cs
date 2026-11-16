using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Formats one species' answer to a field of a per-species question for the questions page: the value the page posts back with the
/// section's answers, and the text the question's table shows for it (the legacy <c>FieldValueBase.ToString()</c>, cut to
/// <see cref="SummaryLength"/> characters).
/// </summary>
public static class ProfileSpeciesAnswerFormatter
{
    /// <summary>How many characters of an answer the table shows, as the legacy table did.</summary>
    public const int SummaryLength = 50;

    /// <summary>Builds the value the page posts for an answer, in the form the page's save reads: <c>true</c> or <c>false</c> for a
    /// Boolean, the identifier of the selected item for a list, the identifiers of the selected items separated by commas for a
    /// multi-value list, a decimal without trailing zeros, a date as day, month and year (which the page only shows, as a per-species
    /// date cannot be edited), and the stored value for any other type. It is empty when the field has no answer.</summary>
    /// <param name="field">The field the answer is for.</param>
    /// <param name="values">The values recorded for the species and the field: several for a multi-value list, otherwise at most one.</param>
    /// <returns>The posted value.</returns>
    public static string FormValue(ProfileFieldMetadataDto field, IReadOnlyList<ProfileFieldValueDto> values)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(values);

        var first = values.Count > 0 ? values[0] : null;

        if (Is(field, "Boolean"))
        {
            return BooleanText(first?.BooleanValue, "true", "false");
        }

        if (Is(field, "List"))
        {
            return SelectedItem(values)?.ToString() ?? string.Empty;
        }

        if (Is(field, "MultiValueList"))
        {
            return string.Join(',', SelectedItems(values));
        }

        if (Is(field, "Decimal"))
        {
            return first?.DecimalValue?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (Is(field, "Date"))
        {
            return first?.DateValue?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return first?.TextValue ?? string.Empty;
    }

    /// <summary>Builds the text the question's table shows for an answer: Yes or No for a Boolean, the text of the selected item for a
    /// list, those of the selected items for a multi-value list, a decimal to two places, a date as day, month and year, and for text
    /// the first <see cref="SummaryLength"/> characters with its markup removed. It is empty when the field has no answer.</summary>
    /// <param name="field">The field the answer is for.</param>
    /// <param name="values">The values recorded for the species and the field.</param>
    /// <param name="options">The items of the field's reference table, which a list answer names.</param>
    /// <returns>The text to show, which the page encodes.</returns>
    public static string SummaryText(ProfileFieldMetadataDto field, IReadOnlyList<ProfileFieldValueDto> values, IReadOnlyList<ReferenceValueDto> options)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(options);

        var first = values.Count > 0 ? values[0] : null;

        if (Is(field, "Boolean"))
        {
            return BooleanText(first?.BooleanValue, "Yes", "No");
        }

        if (Is(field, "List"))
        {
            var selected = SelectedItem(values);

            return options.FirstOrDefault(option => option.Id == selected)?.Value ?? string.Empty;
        }

        if (Is(field, "MultiValueList"))
        {
            var selected = SelectedItems(values);

            return string.Join(", ", options.Where(option => selected.Contains(option.Id)).Select(option => option.Value));
        }

        if (Is(field, "Decimal"))
        {
            return first?.DecimalValue?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (Is(field, "Date"))
        {
            return first?.DateValue?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        }

        var text = ProfileTitleHtmlFormatter.ToPlainText(first?.TextValue);

        return text.Length <= SummaryLength ? text : text[..SummaryLength];
    }

    private static bool Is(ProfileFieldMetadataDto field, string dataType) =>
        string.Equals(field.DataTypeName, dataType, StringComparison.OrdinalIgnoreCase);

    private static string BooleanText(bool? value, string trueText, string falseText)
    {
        if (value is not { } flag)
        {
            return string.Empty;
        }

        return flag ? trueText : falseText;
    }

    private static Guid? SelectedItem(IReadOnlyList<ProfileFieldValueDto> values) =>
        values.Select(value => value.ListValue).FirstOrDefault(id => id is { } selected && selected != Guid.Empty);

    private static List<Guid> SelectedItems(IReadOnlyList<ProfileFieldValueDto> values) =>
        [.. values.Select(value => value.ListValue).OfType<Guid>().Where(id => id != Guid.Empty).Distinct()];
}
