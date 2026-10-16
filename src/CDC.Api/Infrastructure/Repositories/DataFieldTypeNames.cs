namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Reconciles the data type names the database holds with the ones the API contract uses. The
/// <c>luDataFieldType</c> table calls the multiple-choice list type "Multi Value List" (with spaces),
/// whereas the contract, and every consumer of it, calls it <c>MultiValueList</c>; left as stored, those
/// fields would be treated as plain text.
/// </summary>
internal static class DataFieldTypeNames
{
    /// <summary>The contract's name for a list field that allows several selected values.</summary>
    public const string MultiValueList = "MultiValueList";

    private const string DatabaseMultiValueList = "Multi Value List";

    /// <summary>Converts a data type name read from the database to the contract's name.</summary>
    /// <param name="dataTypeName">The name as the database holds it.</param>
    /// <returns>The contract's name for the data type.</returns>
    public static string ToContractName(string dataTypeName) =>
        string.Equals(dataTypeName, DatabaseMultiValueList, StringComparison.OrdinalIgnoreCase) ? MultiValueList : dataTypeName;
}
