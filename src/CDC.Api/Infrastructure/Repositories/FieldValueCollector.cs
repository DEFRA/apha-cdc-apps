namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Collects the field values a procedure returns as the legacy site read them. When a single-value field has been stored more than once
/// for the same species or row (the migrated data does this for the "Include extra epidemiology questions" answer of nearly every version),
/// the last row the database returned is the answer: the legacy page took it, and it is the row <c>spuProfileVersionFieldValue</c> changes.
/// A list value is always kept, for a multi-value list has a row for each item selected.
/// </summary>
/// <typeparam name="T">The type of a field value.</typeparam>
internal sealed class FieldValueCollector<T>
{
    private readonly Dictionary<(Guid FieldId, Guid? SpeciesId, Guid? RowId), int> positionOfSingleValue = [];

    /// <summary>Gets the values collected, in the order their fields' first rows were returned.</summary>
    public List<T> Values { get; } = [];

    /// <summary>Adds a field value, in place of the one already collected for the same field, species and row when neither is a list value.</summary>
    /// <param name="fieldId">The field answered.</param>
    /// <param name="speciesId">The species answered for, if any.</param>
    /// <param name="rowId">The row of a repeating question answered for, if any.</param>
    /// <param name="isListValue">Whether the value is an item of a list.</param>
    /// <param name="value">The value.</param>
    public void Add(Guid fieldId, Guid? speciesId, Guid? rowId, bool isListValue, T value)
    {
        if (isListValue)
        {
            Values.Add(value);

            return;
        }

        var key = (fieldId, speciesId, rowId);

        if (positionOfSingleValue.TryGetValue(key, out var position))
        {
            Values[position] = value;

            return;
        }

        positionOfSingleValue[key] = Values.Count;
        Values.Add(value);
    }
}
