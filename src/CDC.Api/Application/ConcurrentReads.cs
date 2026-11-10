namespace CDC.Api.Application;

/// <summary>
/// Runs independent database reads a few at a time rather than one after another, for the places that read the same kind of
/// data for every section of a profile version.
/// </summary>
internal static class ConcurrentReads
{
    /// <summary>How many reads run at once: enough to overlap the database round trips without one request taking more than a few connections from the pool.</summary>
    private const int MaxConcurrentReads = 4;

    /// <summary>Runs <paramref name="read"/> for each item.</summary>
    /// <typeparam name="TItem">The type of item each read is for.</typeparam>
    /// <typeparam name="TResult">The type each read returns.</typeparam>
    /// <param name="items">The items to read for.</param>
    /// <param name="read">The read to run for one item.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Each read's result, in the same order as <paramref name="items"/>.</returns>
    public static async Task<IReadOnlyList<TResult>> SelectAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, CancellationToken, Task<TResult>> read,
        CancellationToken cancellationToken)
    {
        var results = new TResult[items.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentReads, CancellationToken = cancellationToken },
            async (index, token) => results[index] = await read(items[index], token));

        return results;
    }
}
