using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking;

namespace CDC.Api.Features.PrioritisationVariables;

/// <summary>
/// Builds the "Export score data" and "Export normalised scores (all categories)" Excel files. Ports the legacy
/// <c>MaintainPrioritisationVariables.BuildScoreDataTable</c> and <c>BuildSummaryDataTable</c>, which wrote the same
/// columns, rows and category header colours to an HTML table served as an Excel file.
/// </summary>
internal static partial class PrioritisationScoreWorkbookBuilder
{
    /// <summary>The content type the legacy page served its Excel export with.</summary>
    public const string ContentType = "application/ms-excel";

    /// <summary>The legacy summary export's file name, with an .xls extension.</summary>
    public const string NormalisedScoresFileName = "NormalisedScoreSummary.xls";

    private const string General = "general";

    // Legacy cell widths, in pixels.
    private const double CellWidth = 80;
    private const double TotalCellWidth = 120;

    private const string CombinedRfiTitle = "Ranking of combined RFI scores ";

    private const string CombinedRfiNote =
        "(calculated by adding scores for public health, welfare, wider society and international trade and then ranking this overall total)";

    private const string CombinedRfiPlusRiskTitle = "Ranking of combined RFI scores plus post mitigation risk score ";

    private const string CombinedRfiPlusRiskNote =
        "(calculated by adding scores for public health, welfare, wider society, international trade and post mitigation risk and then ranking this overall total)";

    // Characters that make Excel treat a cell as a formula.
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    private static readonly string[] ProfileHeaders =
        ["Disease / Issue", "Version number", "Profiled species", "Published / Draft", "Profile status", "Standard / Scenario"];

    /// <summary>Builds the score data for one category, or returns <see langword="null"/> when the category does not exist.</summary>
    /// <param name="data">The score data.</param>
    /// <param name="categoryId">The category to export.</param>
    /// <returns>The Excel file, or <see langword="null"/>.</returns>
    public static PrioritisationWorkbookExport? BuildCategoryScoreData(PrioritisationScoreData data, Guid categoryId)
    {
        var category = data.Categories.FirstOrDefault(candidate => candidate.Id == categoryId);

        if (category is null)
        {
            return null;
        }

        var questions = data.PrioritisationQuestions.Where(question => question.CategoryId == categoryId).ToList();
        var style = category.Name.ToLowerInvariant();
        var rows = new List<List<string>>();

        var header = ProfileHeaders.Select(text => new HeaderCell(text, style)).ToList();
        header.AddRange(data.CommonQuestions.Select(question => new HeaderCell(QuestionHeader(question.Name, question.DisplayQuestionNumber), style)));

        foreach (var question in questions)
        {
            header.Add(new HeaderCell(QuestionHeader(question.Name, question.DisplayQuestionNumber), style));
            header.Add(new HeaderCell($"Weighted score for {question.DisplayQuestionNumber}", style));
        }

        header.Add(new HeaderCell("Normalised score", style));

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
            rows.Add(cells);
        }

        return new PrioritisationWorkbookExport(BuildFileName(category.Name), WriteWorkbook(header, rows));
    }

    /// <summary>Builds the normalised score and rank of every category, plus the combined RFI rankings.</summary>
    /// <param name="data">The score data.</param>
    /// <param name="rankedProfiles">Every ranked profile, as read for the disease ranking reports.</param>
    /// <returns>The Excel file.</returns>
    public static PrioritisationWorkbookExport BuildNormalisedScores(PrioritisationScoreData data, IReadOnlyList<RankedProfile> rankedProfiles)
    {
        var rows = new List<List<string>>();

        var header = ProfileHeaders.Select(text => new HeaderCell(text, General)).ToList();
        header.AddRange(data.CommonQuestions.Select(question => new HeaderCell(QuestionHeader(question.Name, question.DisplayQuestionNumber), General)));

        foreach (var categoryName in data.Categories.Select(category => category.Name))
        {
            header.Add(new HeaderCell($"{categoryName} normalised score", categoryName.ToLowerInvariant()));
            header.Add(new HeaderCell($"{categoryName} rank", categoryName.ToLowerInvariant()));
        }

        header.Add(new HeaderCell(CombinedRfiTitle, General, CombinedRfiNote, TotalCellWidth));
        header.Add(new HeaderCell(CombinedRfiPlusRiskTitle, General, CombinedRfiPlusRiskNote, TotalCellWidth));

        var categoryRanks = data.Categories.ToDictionary(category => category.Id, category => BuildCategoryRanks(category.Name, rankedProfiles));
        var totalRanks = BuildTotalRanks(rankedProfiles, RankCategory.Total);
        var totalIncludingRiskRanks = BuildTotalRanks(rankedProfiles, RankCategory.TotalIncludingRisk);

        foreach (var row in BuildRows(data))
        {
            var cells = BuildProfileCells(data, row);

            foreach (var categoryId in data.Categories.Select(category => category.Id))
            {
                cells.Add(FormatNormalisedScore(data, row, categoryId));

                // Scenarios are never ranked, and the legacy rank lists only cover the reasons for intervention and risk.
                var ranks = categoryRanks[categoryId];
                cells.Add(!row.Version.IsScenario && ranks is not null
                    ? FormatRank(ranks.GetValueOrDefault((row.Version.Id, row.SpeciesId)))
                    : string.Empty);
            }

            cells.Add(row.Version.IsScenario ? string.Empty : FormatRank(totalRanks.GetValueOrDefault((row.ProfileId, row.SpeciesId))));
            cells.Add(row.Version.IsScenario ? string.Empty : FormatRank(totalIncludingRiskRanks.GetValueOrDefault((row.ProfileId, row.SpeciesId))));
            rows.Add(cells);
        }

        return new PrioritisationWorkbookExport(NormalisedScoresFileName, WriteWorkbook(header, rows));
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

    // The legacy page rendered an ASP.NET Table to HTML and served it to Excel as a .xls file.
    private static byte[] WriteWorkbook(IReadOnlyList<HeaderCell> header, List<List<string>> rows)
    {
        var html = new StringBuilder("<html><head><meta charset=\"utf-8\"></head><body><table border=\"1\" style=\"border-collapse:collapse\"><tr>");

        foreach (var headerCell in header)
        {
            var foreColor = headerCell.Style == "post mitigation risk" ? "White" : "Black";

            // A td, as the legacy TableCell was: Excel bottom-aligns it, where a th would be centred.
            html.Append(CultureInfo.InvariantCulture,
                $"<td style=\"width:{headerCell.Width}px;background-color:{BackColor(headerCell.Style)};color:{foreColor};font-family:Arial;font-size:10pt;font-weight:bold;white-space:normal;vertical-align:bottom\">")
                .Append(ToCellHtml(headerCell.Text));

            if (headerCell.Note is not null)
            {
                html.Append("<span style=\"font-size:12px\">").Append(WebUtility.HtmlEncode(headerCell.Note)).Append("</span>");
            }

            html.Append("</td>");
        }

        html.Append("</tr>");

        foreach (var row in rows)
        {
            html.Append("<tr>");

            foreach (var value in row)
            {
                // Excel evaluates imported cell text that starts with these characters as a formula unless it is forced to text.
                var text = Clean(value);
                var textFormat = text.Length > 0 && FormulaPrefixes.Contains(text[0]) ? "mso-number-format:'\\@';" : string.Empty;
                html.Append(CultureInfo.InvariantCulture, $"<td style=\"width:{CellWidth}px;{textFormat}font-family:Arial;font-size:10pt;white-space:nowrap;vertical-align:bottom\">")
                    .Append(ToCellHtml(value))
                    .Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</table></body></html>");

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(html.ToString())];
    }

    // Titles, species and question names are stored with italic markup, which the legacy cells showed; any other markup is dropped.
    private static string ToCellHtml(string value)
    {
        var html = new StringBuilder();
        var position = 0;

        foreach (var tag in MarkupRegex().Matches(value).Cast<Match>())
        {
            html.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(value[position..tag.Index])));
            position = tag.Index + tag.Length;

            var italic = ItalicTagRegex().Match(tag.Value);

            if (italic.Success)
            {
                html.Append(italic.Groups[1].Length > 0 ? "</i>" : "<i>");
            }
        }

        return html.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(value[position..]))).ToString();
    }

    // The text of a cell without markup, as Excel reads it.
    private static string Clean(string value) => WebUtility.HtmlDecode(MarkupRegex().Replace(value, string.Empty));

    // The legacy category header colours.
    private static string BackColor(string style) => style switch
    {
        "international trade" => "rgb(255,255,189)",
        "wider society" => "rgb(153,204,255)",
        "welfare" => "rgb(193,255,193)",
        "public health" => "rgb(255,153,204)",
        "post mitigation risk" => "rgb(0,0,0)",
        General => "rgb(200,200,200)",
        _ => "rgb(102,102,0)"
    };

    private static string BuildFileName(string categoryName)
    {
        var safeName = new string([.. categoryName.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-' or '_')]).Trim();

        return $"{(safeName.Length == 0 ? "ScoreData" : safeName)}.xls";
    }

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex MarkupRegex();

    [GeneratedRegex(@"^<(/?)(?:em|i)\s*>$", RegexOptions.IgnoreCase)]
    private static partial Regex ItalicTagRegex();

    private sealed record HeaderCell(string Text, string Style, string? Note = null, double Width = CellWidth);

    private sealed record ExportRow(ScoreDataProfile Profile, ScoreDataProfileVersion Version, Guid SpeciesId, string SpeciesName)
    {
        public Guid ProfileId => Profile.Id;
    }
}
