using System.Data.Common;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Reads a single metadata result set from a multi-result-set <see cref="DbDataReader"/> into a
/// list, shared by <see cref="SpeciesRepository"/> and <see cref="ProfileSectionRepository"/>
/// since both read their questionnaire metadata procedures positionally in the same shape.
/// </summary>
internal static class MetadataResultSetReader
{
    /// <summary>Reads every row of the reader's current result set, mapping each with <paramref name="map"/>.</summary>
    public static async Task<List<T>> ReadCurrentResultSetAsync<T>(
        DbDataReader reader,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var rows = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    /// <summary>Advances to the reader's next result set and reads it, or returns an empty list if there isn't one.</summary>
    public static async Task<List<T>> ReadNextResultSetAsync<T>(
        DbDataReader reader,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken))
        {
            return [];
        }

        return await ReadCurrentResultSetAsync(reader, map, cancellationToken);
    }
}
