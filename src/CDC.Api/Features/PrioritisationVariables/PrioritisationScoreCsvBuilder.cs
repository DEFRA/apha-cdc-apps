using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking;

namespace CDC.Api.Features.PrioritisationVariables;

/// <summary>
/// Builds the "Export score data" and "Export normalised scores (all categories)" CSV files. Ports the legacy
/// <c>MaintainPrioritisationVariables.BuildScoreDataTable</c> and <c>BuildSummaryDataTable</c>, which wrote the same
/// columns and rows to an HTML table served as an Excel file.
/// </summary>
internal static partial class PrioritisationScoreCsvBuilder
{
    /// <summary>The legacy summary export's file name, with a CSV extension.</summary>
    public const string NormalisedScoresFileName = "NormalisedScoreSummary.csv";

    private const string CombinedRfiHeader =
        "Ranking of combined RFI scores (calculated by adding scores for public health, welfare, wider society and international trade and then ranking this overall total)";

    private const string CombinedRfiPlusRiskHeader =
        "Ranking of combined RFI scores plus post mitigation risk score (calculated by adding scores for public health, welfare, wider society, international trade and post mitigation risk and then ranking this overall total)";

    private static readonly string[] ProfileHeaders =
        ["Disease / Issue", "Version number", "Profiled species", "Published / Draft", "Profile status", "Standard / Scenario"];

    // Characters that make Excel treat a cell as a formula (CSV injection).
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Builds the score data for one category, or returns <see langword="null"/> when the category does not exist.</summary>
    /// <param name="data">The score data.</param>
    /// <param name="categoryId">The category to export.</param>
    /// <returns>The CSV file, or <see langword="null"/>.</returns>
    public static PrioritisationCsvExport? BuildCategoryScoreData(PrioritisationScoreData data, Guid categoryId)
    {
        var category = data.Categories.FirstOrDefault(candidate => candidate.Id == categoryId);

        if (category is null)
        {
            return null;
        }

        var questions = data.PrioritisationQuestions.Where(question => question.CategoryId == categoryId).ToList();
        var csv = new StringBuilder();

        var header = new List<string>(ProfileHeaders);
        header.AddRange(data.CommonQuestions.Select(question => QuestionHeader(question.Name, question.DisplayQuestionNumber)));

        foreach (var question in questions)
        {
            header.Add(QuestionHeader(question.Name, question.DisplayQuestionNumber));
            header.Add($"Weighted score for {question.DisplayQuestionNumber}");
        }

        header.Add("Normalised score");
        AppendRow(csv, header);

        foreach (var row in BuildRows(data))
        {
            var cells = BuildProfileCells(data, row);

            foreach (var question in questions)
            {
                var speciesId = question.IsPerSpecies ? row.SpeciesId : Guid.Empty;
                var answer = data.PrioritisationAnswers.FirstOrDefault(candidate =>
                    candidate.QuestionId == question.Id && candidate.ProfileVersionId == row.Version.Id && candidate.SpeciesId == speciesId);

                cells.Add(answer?.Answer ?? string.Empty);
                cells.Add(answer is null ? string.Empty : Format(answer.WeightedScore));
            }

            cells.Add(FormatNormalisedScore(data, row, categoryId));
            AppendRow(csv, cells);
        }

        return new PrioritisationCsvExport(BuildFileName(category.Name), Encode(csv));
    }

    /// <summary>Builds the normalised score and rank of every category, plus the combined RFI rankings.</summary>
    /// <param name="data">The score data.</param>
    /// <param name="rankedProfiles">Every ranked profile, as read for the disease ranking reports.</param>
    /// <returns>The CSV file.</returns>
    public static PrioritisationCsvExport BuildNormalisedScores(PrioritisationScoreData data, IReadOnlyList<RankedProfile> rankedProfiles)
    {
        var csv = new StringBuilder();

        var header = new List<string>(ProfileHeaders);
        header.AddRange(data.CommonQuestions.Select(question => QuestionHeader(question.Name, question.DisplayQuestionNumber)));

        foreach (var category in data.Categories)
        {
            header.Add($"{category.Name} normalised score");
            header.Add($"{category.Name} rank");
        }

        header.Add(CombinedRfiHeader);
        header.Add(CombinedRfiPlusRiskHeader);
        AppendRow(csv, header);

        var categoryRanks = data.Categories.ToDictionary(category => category.Id, category => BuildCategoryRanks(category.Name, rankedProfiles));
        var totalRanks = BuildTotalRanks(rankedProfiles, RankCategory.Total);
        var totalIncludingRiskRanks = BuildTotalRanks(rankedProfiles, RankCategory.TotalIncludingRisk);

        foreach (var row in BuildRows(data))
        {
            var cells = BuildProfileCells(data, row);

            foreach (var category in data.Categories)
            {
                cells.Add(FormatNormalisedScore(data, row, category.Id));

                // Scenarios are never ranked, and the legacy rank lists only cover the reasons for intervention and risk.
                var ranks = categoryRanks[category.Id];
                cells.Add(!row.Version.IsScenario && ranks is not null
                    ? FormatRank(ranks.GetValueOrDefault((row.Version.Id, row.SpeciesId)))
                    : string.Empty);
            }

            cells.Add(row.Version.IsScenario ? string.Empty : FormatRank(totalRanks.GetValueOrDefault((row.ProfileId, row.SpeciesId))));
            cells.Add(row.Version.IsScenario ? string.Empty : FormatRank(totalIncludingRiskRanks.GetValueOrDefault((row.ProfileId, row.SpeciesId))));
            AppendRow(csv, cells);
        }

        return new PrioritisationCsvExport(NormalisedScoresFileName, Encode(csv));
    }

    /// <summary>
    /// One row per profile, profiled species and version holding that species - the legacy
    /// <c>GetProfileSpeciesList</c> / <c>GetProfileVersionListBySpeciesId</c> loops.
    /// </summary>
    private static IEnumerable<ExportRow> BuildRows(PrioritisationScoreData data)
    {
        foreach (var profile in data.Profiles)
        {
            var versions = data.ProfileVersions.Where(version => version.ProfileId == profile.Id).ToList();
            var speciesByVersion = versions.ToDictionary(
                version => version.Id,
                version => data.ProfiledSpecies.Where(species => species.ProfileVersionId == version.Id).ToList());
            var speciesIds = versions.SelectMany(version => speciesByVersion[version.Id]).Select(species => species.SpeciesId).Distinct();

            foreach (var speciesId in speciesIds)
            {
                foreach (var version in versions)
                {
                    var species = speciesByVersion[version.Id].FirstOrDefault(candidate => candidate.SpeciesId == speciesId);

                    if (species is not null)
                    {
                        yield return new ExportRow(profile, version, speciesId, species.Name);
                    }
                }
            }
        }
    }

    private static List<string> BuildProfileCells(PrioritisationScoreData data, ExportRow row)
    {
        var cells = new List<string>
        {
            row.Version.IsScenario ? $"{row.Profile.Title} ({row.Version.ScenarioTitle})" : row.Profile.Title,
            row.Version.DisplayVersionNumber,
            row.SpeciesName,
            row.Version.State,
            row.Profile.StatusName,
            row.Version.IsScenario ? "Scenario" : "Standard"
        };

        foreach (var question in data.CommonQuestions)
        {
            var answer = data.CommonAnswers.FirstOrDefault(candidate =>
                candidate.QuestionId == question.Id && candidate.ProfileVersionId == row.Version.Id);
            cells.Add(answer?.Answer ?? string.Empty);
        }

        return cells;
    }

    private static string FormatNormalisedScore(PrioritisationScoreData data, ExportRow row, Guid categoryId)
    {
        var score = data.NormalisedScores.FirstOrDefault(candidate =>
            candidate.ProfileVersionId == row.Version.Id && candidate.SpeciesId == row.SpeciesId && candidate.CategoryId == categoryId);

        return score is null ? string.Empty : Format(score.Rank);
    }

    /// <summary>Ranks one category's diseases with an impact, keyed by the version the rank was read from and the species.</summary>
    private static Dictionary<(Guid ProfileVersionId, Guid SpeciesId), int>? BuildCategoryRanks(string categoryName, IReadOnlyList<RankedProfile> rankedProfiles)
    {
        (RankCategory Category, Func<RankedProfile, Guid?> VersionId)? mapping = categoryName.ToLowerInvariant() switch
        {
            "international trade" => (RankCategory.InternationalTrade, profile => profile.InternationalTradeProfileVersionId),
            "public health" => (RankCategory.PublicHealth, profile => profile.PublicHealthProfileVersionId),
            "welfare" => (RankCategory.AnimalWelfare, profile => profile.AnimalWelfareProfileVersionId),
            "wider society" => (RankCategory.WiderSociety, profile => profile.WiderSocietyProfileVersionId),
            "post mitigation risk" => (RankCategory.Risk, profile => profile.RiskProfileVersionId),
            _ => null
        };

        if (mapping is not { } known)
        {
            return null;
        }

        var (items, sources) = CreateItems(rankedProfiles);
        var ranked = RankListOperations.WithImpact(RankListOperations.GetFilteredSortedList(items, null, known.Category), known.Category);
        RankListOperations.CalculateRankPosition(ranked, known.Category);

        var positions = new Dictionary<(Guid, Guid), int>();

        foreach (var item in ranked)
        {
            var source = sources[item];

            if (known.VersionId(source) is { } profileVersionId)
            {
                positions.TryAdd((profileVersionId, source.SpeciesId), item.RankPosition);
            }
        }

        return positions;
    }

    /// <summary>Ranks every disease by a combined total, keyed by profile and species.</summary>
    private static Dictionary<(Guid ProfileId, Guid SpeciesId), int> BuildTotalRanks(IReadOnlyList<RankedProfile> rankedProfiles, RankCategory category)
    {
        var (items, sources) = CreateItems(rankedProfiles);
        var ranked = RankListOperations.GetFilteredSortedList(items, null, category);
        RankListOperations.CalculateRankPosition(ranked, category);

        var positions = new Dictionary<(Guid, Guid), int>();

        foreach (var item in ranked)
        {
            positions.TryAdd((item.ProfileId, sources[item].SpeciesId), item.RankPosition);
        }

        return positions;
    }

    // Rank positions are written onto the items, so every ranking gets fresh ones, as the legacy code re-read the list each time.
    private static (List<RankItem> Items, Dictionary<RankItem, RankedProfile> Sources) CreateItems(IReadOnlyList<RankedProfile> rankedProfiles)
    {
        var sources = rankedProfiles.ToDictionary(profile => new RankItem(profile), profile => profile);

        return ([.. sources.Keys], sources);
    }

    private static string QuestionHeader(string name, string displayQuestionNumber) => $"{name} ({displayQuestionNumber})";

    private static string FormatRank(int rank) => rank > 0 ? Format(rank) : string.Empty;

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void AppendRow(StringBuilder csv, IEnumerable<string> cells)
    {
        csv.AppendJoin(',', cells.Select(Escape));
        csv.Append("\r\n");
    }

    private static string Escape(string value)
    {
        var text = WebUtility.HtmlDecode(MarkupRegex().Replace(value, string.Empty));

        if (text.Length > 0 && Array.IndexOf(FormulaPrefixes, text[0]) >= 0)
        {
            text = "'" + text;
        }

        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static byte[] Encode(StringBuilder csv) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];

    private static string BuildFileName(string categoryName)
    {
        var safeName = new string([.. categoryName.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_')]).Trim();

        return $"{(safeName.Length == 0 ? "ScoreData" : safeName)}.csv";
    }

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex MarkupRegex();

    private sealed record ExportRow(ScoreDataProfile Profile, ScoreDataProfileVersion Version, Guid SpeciesId, string SpeciesName)
    {
        public Guid ProfileId => Profile.Id;
    }
}
