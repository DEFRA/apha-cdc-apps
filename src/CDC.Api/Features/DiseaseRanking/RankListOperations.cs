namespace CDC.Api.Features.DiseaseRanking;

/// <summary>
/// Describes one reason for intervention (RFI) so list operations can treat all four alike.
/// </summary>
/// <param name="Category">The category the RFI represents.</param>
/// <param name="Name">The display name used in chart legends.</param>
/// <param name="Rank">Reads the RFI's rank.</param>
/// <param name="Percentile">Reads the percentile rank within a list.</param>
/// <param name="SetPercentile">Writes the percentile rank.</param>
/// <param name="SetCombined">Writes the RFI percentile plus the post mitigation risk percentile.</param>
/// <param name="Combined">Reads the RFI percentile plus the post mitigation risk percentile.</param>
internal sealed record Rfi(
    RankCategory Category,
    string Name,
    Func<RankItem, int> Rank,
    Func<RankItem, double> Percentile,
    Action<RankItem, double> SetPercentile,
    Func<RankItem, double> Combined,
    Action<RankItem, double> SetCombined)
{
    public static readonly Rfi PublicHealth = new(
        RankCategory.PublicHealth, "Public health",
        item => item.PublicHealthRank,
        item => item.PublicHealthPmrRank, (item, value) => item.PublicHealthPmrRank = value,
        item => item.PublicHealthAndPmrRank, (item, value) => item.PublicHealthAndPmrRank = value);

    public static readonly Rfi AnimalWelfare = new(
        RankCategory.AnimalWelfare, "Welfare",
        item => item.AnimalWelfareRank,
        item => item.AnimalWelfarePmrRank, (item, value) => item.AnimalWelfarePmrRank = value,
        item => item.AnimalWelfareAndPmrRank, (item, value) => item.AnimalWelfareAndPmrRank = value);

    public static readonly Rfi WiderSociety = new(
        RankCategory.WiderSociety, "Wider society",
        item => item.WiderSocietyRank,
        item => item.WiderSocietyPmrRank, (item, value) => item.WiderSocietyPmrRank = value,
        item => item.WiderSocietyAndPmrRank, (item, value) => item.WiderSocietyAndPmrRank = value);

    public static readonly Rfi InternationalTrade = new(
        RankCategory.InternationalTrade, "International trade",
        item => item.InternationalTradeRank,
        item => item.InternationalTradePmrRank, (item, value) => item.InternationalTradePmrRank = value,
        item => item.InternationalTradeAndPmrRank, (item, value) => item.InternationalTradeAndPmrRank = value);

    /// <summary>Gets the four RFIs in the order the front page lists them.</summary>
    public static IReadOnlyList<Rfi> All { get; } = [PublicHealth, AnimalWelfare, WiderSociety, InternationalTrade];

    public static Rfi For(RankCategory category) => All.First(rfi => rfi.Category == category);
}

/// <summary>
/// Ports of the legacy <c>RankProfileInfoList</c> ordering and ranking operations. Every method
/// keeps the legacy tie-breaking so reports order diseases identically.
/// </summary>
internal static class RankListOperations
{
    // Legacy VB sorted strings with the default (culture-sensitive) comparer.
    private static readonly StringComparer TitleComparer = StringComparer.InvariantCulture;

    /// <summary>Filters by exotic/endemic, optionally keeps one species per profile, then sorts ascending by <paramref name="category"/>.</summary>
    public static List<RankItem> GetFilteredSortedList(
        IEnumerable<RankItem> all,
        bool? isExotic,
        RankCategory category,
        bool onePerDisease = false)
    {
        var output = isExotic.HasValue
            ? all.Where(item => item.IsExotic == isExotic.Value).ToList()
            : [.. all];

        if (onePerDisease)
        {
            output = KeepHighestSpeciesPerProfile(output, category);
        }

        return SortAscending(output, category);
    }

    /// <summary>
    /// Recomputes post mitigation risk and per-RFI percentile ranks across <paramref name="all"/>,
    /// then returns the list for <paramref name="category"/>. Overwrites the percentile ranks on the
    /// shared items, exactly as the legacy <c>GetAllSortedList</c> did.
    /// </summary>
    public static List<RankItem> GetAllSortedList(IEnumerable<RankItem> all, RankCategory category, bool onePerDisease = false)
    {
        var current = all.OrderByDescending(item => item.PositiveRiskRank).ToList();
        AssignPercentiles(current, item => item.PositiveRiskRank, (item, value) => item.PmrRank = value);

        // Each pass re-sorts the previous pass's result, as the legacy code did.
        foreach (var rfi in Rfi.All)
        {
            current = [.. current.OrderByDescending(rfi.Rank)];
            AssignPercentiles(current, rfi.Rank, rfi.SetPercentile);
        }

        var output = current;

        if (onePerDisease)
        {
            output = KeepHighestSpeciesPerProfile(output, category);
        }

        return category switch
        {
            RankCategory.Risk => RankByRisk(output),
            RankCategory.Total or RankCategory.TotalIncludingRisk => SortAscending(output, category),
            _ => RankByRfi(output, Rfi.For(category))
        };
    }

    /// <summary>Assigns the competition-style rank position used by the top 25 tables.</summary>
    public static void CalculateRankPosition(IReadOnlyList<RankItem> ascending, RankCategory category)
    {
        var currentRank = 0;
        var currentRiskRank = 0;
        var rankPosition = 1;
        var offset = 0;
        var isFirst = true;

        for (var index = ascending.Count - 1; index >= 0; index--)
        {
            var item = ascending[index];
            var nextRank = item.GetRank(category);
            var nextRiskRank = item.PositiveRiskRank;

            if (isFirst)
            {
                currentRank = nextRank;
                currentRiskRank = nextRiskRank;
                item.RankPosition = 1;
                isFirst = false;
            }
            else if (nextRank < currentRank || (nextRank == currentRank && nextRiskRank < currentRiskRank))
            {
                rankPosition += offset + 1;
                item.RankPosition = rankPosition;
                currentRank = nextRank;
                currentRiskRank = nextRiskRank;
                offset = 0;
            }
            else if (nextRank == currentRank)
            {
                item.RankPosition = rankPosition;
                currentRiskRank = nextRiskRank;
                offset++;
            }
        }
    }

    /// <summary>Drops diseases with a zero rank for <paramref name="category"/>.</summary>
    public static List<RankItem> WithImpact(IEnumerable<RankItem> items, RankCategory category) =>
        [.. items.Where(item => item.GetRank(category) > 0)];

    /// <summary>Gets the highest total, used to size a scored chart's axis.</summary>
    public static int MaxTotalRank(IReadOnlyList<RankItem> items) =>
        items.Count == 0 ? 0 : items.Max(item => item.TotalRank);

    /// <summary>Gets the highest total including risk, used to size a scored chart's axis.</summary>
    public static int MaxTotalIncludingRiskRank(IReadOnlyList<RankItem> items) =>
        items.Count == 0 ? 0 : items.Max(item => item.TotalIncludingRiskRank);

    /// <summary>Splits an ascending list into pages of <paramref name="size"/>, highest-ranked page first.</summary>
    public static List<List<RankItem>> ChunkFromTop(IReadOnlyList<RankItem> ascending, int size)
    {
        var chunks = new List<List<RankItem>>();

        for (var end = ascending.Count; end > 0; end -= size)
        {
            var start = Math.Max(end - size, 0);
            chunks.Add([.. ascending.Skip(start).Take(end - start)]);
        }

        return chunks;
    }

    private static List<RankItem> KeepHighestSpeciesPerProfile(IEnumerable<RankItem> items, RankCategory category)
    {
        var byProfile = new Dictionary<Guid, RankItem>();

        foreach (var item in items)
        {
            if (!byProfile.TryGetValue(item.ProfileId, out var existing) || existing.GetRank(category) < item.GetRank(category))
            {
                byProfile[item.ProfileId] = item;
            }
        }

        return [.. byProfile.Values];
    }

    private static List<RankItem> SortAscending(List<RankItem> items, RankCategory category) => category switch
    {
        RankCategory.Risk => [.. items.OrderBy(item => item.PositiveRiskRank).ThenByDescending(item => item.FullTitleWithoutAsterisk, TitleComparer)],
        RankCategory.TotalIncludingRisk => [.. items.OrderBy(item => item.TotalIncludingRiskRank).ThenByDescending(item => item.FullTitleWithoutAsterisk, TitleComparer)],
        _ => [.. items.OrderBy(item => item.GetRank(category)).ThenBy(item => item.PositiveRiskRank).ThenByDescending(item => item.FullTitleWithoutAsterisk, TitleComparer)]
    };

    private static List<RankItem> RankByRfi(List<RankItem> items, Rfi rfi)
    {
        var output = items.Where(item => rfi.Rank(item) != 0).OrderByDescending(rfi.Percentile).ToList();

        AssignPercentiles(output, rfi.Rank, rfi.SetPercentile, item => rfi.SetCombined(item, rfi.Percentile(item) + item.PmrRank));

        return [.. output.OrderBy(rfi.Combined).ThenByDescending(item => item.FullTitleWithoutAsterisk, TitleComparer)];
    }

    private static List<RankItem> RankByRisk(List<RankItem> items)
    {
        var output = items.Where(item => item.PositiveRiskRank != 0).OrderByDescending(item => item.PmrRank).ToList();

        AssignPercentiles(output, item => item.PositiveRiskRank, (item, value) => item.PmrRank = value);

        return [.. output.OrderBy(item => item.PmrRank).ThenByDescending(item => item.FullTitleWithoutAsterisk, TitleComparer)];
    }

    // Walks a descending list giving each item with a non-zero rank a percentile of the list size.
    private static void AssignPercentiles(
        List<RankItem> descending,
        Func<RankItem, int> rank,
        Action<RankItem, double> setPercentile,
        Action<RankItem>? afterAssign = null)
    {
        var count = descending.Count;
        var position = 0;

        foreach (var item in descending.Where(item => rank(item) > 0))
        {
            setPercentile(item, (count - position) * 100.0 / count);
            afterAssign?.Invoke(item);
            position++;
        }
    }
}
