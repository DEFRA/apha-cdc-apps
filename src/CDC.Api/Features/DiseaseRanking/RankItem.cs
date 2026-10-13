using System.Net;
using System.Text.RegularExpressions;
using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.DiseaseRanking;

/// <summary>The reasons for intervention (RFIs) and totals a ranking can be ordered by.</summary>
internal enum RankCategory
{
    PublicHealth,
    AnimalWelfare,
    WiderSociety,
    InternationalTrade,
    Risk,
    Total,
    TotalIncludingRisk
}

/// <summary>
/// A <see cref="RankedProfile"/> plus the percentile ranks the legacy <c>RankProfileInfo</c>
/// computed and overwrote on the shared instances while a report was built. The mutable state is
/// deliberate: the "ranked" report's bar values depend on the order the legacy code mutated them.
/// </summary>
/// <param name="source">The ranks read from the database.</param>
internal sealed partial class RankItem(RankedProfile source)
{
    private const int LabelLength = 50;

    public Guid ProfileId => source.ProfileId;

    public bool IsExotic => source.IsExotic;

    public int PublicHealthRank => source.PublicHealthRank;

    public int AnimalWelfareRank => source.AnimalWelfareRank;

    public int WiderSocietyRank => source.WiderSocietyRank;

    public int InternationalTradeRank => source.InternationalTradeRank;

    public int PositiveRiskRank => source.RiskRank;

    /// <summary>Gets the risk rank negated, so scored charts can draw it left of the axis origin.</summary>
    public int RiskRank => -source.RiskRank;

    public int TotalRank => PublicHealthRank + AnimalWelfareRank + WiderSocietyRank + InternationalTradeRank;

    public int TotalIncludingRiskRank => TotalRank + PositiveRiskRank;

    public double PmrRank { get; set; }

    public double PublicHealthPmrRank { get; set; }

    public double AnimalWelfarePmrRank { get; set; }

    public double WiderSocietyPmrRank { get; set; }

    public double InternationalTradePmrRank { get; set; }

    public double PublicHealthAndPmrRank { get; set; }

    public double AnimalWelfareAndPmrRank { get; set; }

    public double WiderSocietyAndPmrRank { get; set; }

    public double InternationalTradeAndPmrRank { get; set; }

    public int RankPosition { get; set; }

    /// <summary>Gets the chart label: exotic diseases in capitals, <c>*</c> when awaiting validation.</summary>
    public string FullTitle => Truncate(BuildTitle(withAsterisk: true), LabelLength);

    public string FullTitleWithoutAsterisk => Truncate(BuildTitle(withAsterisk: false), LabelLength);

    /// <summary>Gets the table label: exotic disease names in capitals, species names left as entered.</summary>
    /// <param name="length">The maximum length.</param>
    public string FullTitleMixedCase(int length) =>
        IsExotic
            ? Truncate(
                $"{(source.IsAwaitingValidation ? "*" : string.Empty)}{Clean(source.ProfileTitle).ToUpperInvariant()} ({Clean(source.SpeciesName)})",
                length)
            : Truncate(BuildTitle(withAsterisk: true), length);

    public int GetRank(RankCategory category) => category switch
    {
        RankCategory.PublicHealth => PublicHealthRank,
        RankCategory.AnimalWelfare => AnimalWelfareRank,
        RankCategory.WiderSociety => WiderSocietyRank,
        RankCategory.InternationalTrade => InternationalTradeRank,
        RankCategory.Risk => PositiveRiskRank,
        RankCategory.Total => TotalRank,
        RankCategory.TotalIncludingRisk => TotalIncludingRiskRank,
        _ => 0
    };

    private static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;

    private static string Clean(string value) =>
        WebUtility.HtmlDecode(MarkupRegex().Replace(value, string.Empty));

    [GeneratedRegex("<.*?>")]
    private static partial Regex MarkupRegex();

    private string BuildTitle(bool withAsterisk)
    {
        var profileTitle = Clean(source.ProfileTitle);
        var speciesName = Clean(source.SpeciesName);
        var prefix = withAsterisk && source.IsAwaitingValidation ? "*" : string.Empty;

        return IsExotic
            ? $"{prefix}{profileTitle.ToUpperInvariant()} ({speciesName.ToUpperInvariant()})"
            : $"{prefix}{profileTitle} ({speciesName})";
    }
}
