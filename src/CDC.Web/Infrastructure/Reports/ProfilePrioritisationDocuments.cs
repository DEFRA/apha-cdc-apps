using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Lays out the summary prioritisation report from a profile version's content and prioritisation
/// scores, following the legacy <c>SummaryPrioritisationPdf</c>: a summary of questions and category
/// ranks, then the detailed criteria scores two species at a time.
/// </summary>
public static class ProfilePrioritisationDocuments
{
    /// <summary>The internal report name of the summary prioritisation report.</summary>
    public const string ReportName = "SummaryPrioritisationGUID";

    /// <summary>The report's title.</summary>
    public const string ReportTitle = "Summary prioritisation report";

    /// <summary>The line shown when prioritisation has not been run for the version.</summary>
    public const string NoDataMessage = "Prioritisation data has not been generated for this profile version.";

    /// <summary>The italic note printed between the post-mitigation ranks and the remaining questions.</summary>
    public const string StandardisedEvidenceNote =
        "The following information is derived from evidence assessed in a standardised way, and is of relevance when considering distribution of funds. However, it is not considered to directly determine the relative importance of animal health issues.";

    private const string IncompleteDataText = "- Data incomplete -";
    private const string IrrelevantCategoryText = "- Irrelevant category -";
    private const string PotentialToAddValue = "Potential to add value";
    private const string PostMitigationRisk = "post mitigation risk";

    /// <summary>Lays the report out.</summary>
    /// <param name="content">The profile version's report content, which supplies the questions.</param>
    /// <param name="scores">The profile version's prioritisation scores.</param>
    /// <returns>The document.</returns>
    public static PrioritisationDocumentViewModel Build(ProfileReportContentDto content, ProfilePrioritisationScoresDto scores)
    {
        var published = content.IsPublished ? content.EffectiveDateFrom.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) : null;
        var hasData = scores.CategoryScores.Count > 0;

        return new PrioritisationDocumentViewModel(
            ReportTitle,
            content.FullTitle,
            content.VersionLabel,
            published,
            content.ShowDraftWatermark,
            hasData,
            hasData ? BuildBlocks(content, scores) : [],
            hasData ? BuildCriteriaPages(scores) : []);
    }

    private static List<PrioritisationBlockView> BuildBlocks(ProfileReportContentDto content, ProfilePrioritisationScoresDto scores)
    {
        var blocks = new List<PrioritisationBlockView>();

        void AddQuestion(int section, int question, string title)
        {
            if (ProfileReportLayout.FindQuestion(content, section, question) is { } found)
            {
                blocks.Add(new PrioritisationBlockView(Question: new ReportQuestionView(title, null, found)));
            }
        }

        void AddCategory(string categoryName) =>
            blocks.Add(new PrioritisationBlockView(CategoryTable: BuildCategoryTable(scores, categoryName)));

        AddQuestion(1, 1, "Summary description");
        AddQuestion(6, 14, "RFI 1 Protect human health");
        AddCategory("Public health");
        AddQuestion(5, 9, "RFI 2 Protect and promote animal welfare");
        AddCategory("Welfare");
        AddQuestion(7, 7, "RFI 3 Protect interests of wider economy, environment & society");
        AddCategory("Wider society");
        AddQuestion(13, 11, "RFI 4 International trade");
        AddCategory("International trade");
        AddQuestion(3, 20, "Post mitigation risk");
        AddCategory("Post mitigation risk");
        blocks.Add(new PrioritisationBlockView(Note: StandardisedEvidenceNote));

        if (scores.CategoryScores.Any(score => IsCategory(score.CategoryName, PotentialToAddValue)))
        {
            AddQuestion(14, 1, "Opportunity to enhance risk mitigation and/or understanding");
            AddCategory(PotentialToAddValue);
        }

        AddQuestion(15, 1, "Public perception");
        AddQuestion(7, 8, "Summary of central government veterinary expenditure");
        AddQuestion(16, 1, "Uncertainty");
        AddQuestion(8, 4, "Legislation");

        return blocks;
    }

    private static PrioritisationCategoryTableView BuildCategoryTable(ProfilePrioritisationScoresDto scores, string categoryName) =>
        new([
            .. scores.CategoryScores
                .Where(score => IsCategory(score.CategoryName, categoryName) && score.IsProfiledSpecies)
                .Select(score => new PrioritisationCategoryRowView(
                    score.SpeciesName,
                    score.DataIncomplete ? IncompleteDataText : score.Rank.ToString(CultureInfo.InvariantCulture),
                    score.DataIncomplete))
        ]);

    private static List<PrioritisationCriteriaPageView> BuildCriteriaPages(ProfilePrioritisationScoresDto scores)
    {
        var profiledSpecies = scores.Species
            .Where(species => IsProfiledSpecies(species.SpeciesId, scores))
            .ToList();
        var pages = new List<PrioritisationCriteriaPageView>();

        for (var index = 0; index < profiledSpecies.Count; index += 2)
        {
            var pair = profiledSpecies.Skip(index).Take(2).ToList();
            var names = pair.Select(species => ProfileTitleHtmlFormatter.ToPlainText(species.Name)).ToList();
            var speciesHeading = string.Join(" and ", names);

            pages.Add(new PrioritisationCriteriaPageView(
                "Detailed criteria scores",
                $"criteria-{pages.Count + 1}",
                speciesHeading,
                [.. pair.Select(species => species.Name)],
                BuildCriteriaCategories(scores, pair)));
        }

        return pages;
    }

    private static List<PrioritisationCriteriaCategoryView> BuildCriteriaCategories(ProfilePrioritisationScoresDto scores, List<ProfilePrioritisedSpeciesDto> pair)
    {
        var first = pair[0].SpeciesId;
        var rows = scores.CriterionScores.Where(score => score.SpeciesId == first).ToList();
        var categories = new List<PrioritisationCriteriaCategoryView>();

        foreach (var categoryRows in rows.GroupBy(row => row.CategoryId))
        {
            var category = categoryRows.First();
            var (background, foreground) = GetCategoryColours(category.CategoryName);

            categories.Add(new PrioritisationCriteriaCategoryView(
                category.CategoryDisplayName,
                background,
                foreground,
                [.. pair.Select(species => BuildRank(scores, category.CategoryId, species.SpeciesId))],
                [
                    .. categoryRows
                        .DistinctBy(row => row.CriterionId)
                        .Select(row => new PrioritisationCriterionRowView(
                            row.CriterionName,
                            [.. pair.Select(species => BuildValue(scores, row.CriterionId, species.SpeciesId))]))
                ]));
        }

        return categories;
    }

    private static PrioritisationValueView BuildRank(ProfilePrioritisationScoresDto scores, Guid categoryId, Guid speciesId)
    {
        var score = scores.CategoryScores.FirstOrDefault(candidate => candidate.CategoryId == categoryId && candidate.SpeciesId == speciesId);

        if (score is null)
        {
            return new PrioritisationValueView(string.Empty, false);
        }

        return score.DataIncomplete
            ? new PrioritisationValueView(IncompleteDataText, true)
            : new PrioritisationValueView(score.Rank.ToString(CultureInfo.InvariantCulture), false);
    }

    private static PrioritisationValueView BuildValue(ProfilePrioritisationScoresDto scores, Guid criterionId, Guid speciesId)
    {
        var score = scores.CriterionScores.FirstOrDefault(candidate => candidate.CriterionId == criterionId && candidate.SpeciesId == speciesId);

        if (score is null)
        {
            return new PrioritisationValueView(string.Empty, false);
        }

        if (score.DataIncomplete)
        {
            return new PrioritisationValueView(IncompleteDataText, true);
        }

        return score.IrrelevantCategory
            ? new PrioritisationValueView(IrrelevantCategoryText, true)
            : new PrioritisationValueView(score.CriterionValue, false);
    }

    /// <summary>Legacy <c>GetCategoryBackColor</c> and <c>GetCategoryForeColor</c>.</summary>
    private static (string Background, string Foreground) GetCategoryColours(string categoryName) =>
        categoryName.ToLowerInvariant() switch
        {
            "international trade" => ("rgb(255, 255, 189)", "#000"),
            "wider society" => ("rgb(153, 204, 255)", "#000"),
            "welfare" => ("rgb(193, 255, 193)", "#000"),
            "public health" => ("rgb(255, 153, 204)", "#000"),
            PostMitigationRisk => ("rgb(0, 0, 0)", "#fff"),
            _ => ("rgb(0, 163, 59)", "#000")
        };

    private static bool IsCategory(string name, string expected) =>
        string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the page header and footer. The legacy footer repeated the report's title, profile, version
    /// and published date, the quality statement and the page number on every page.
    /// </summary>
    /// <param name="document">The document the options are for.</param>
    /// <returns>The render options.</returns>
    public static PdfRenderOptions BuildPdfOptions(PrioritisationDocumentViewModel document)
    {
        var published = document.PublishedDate is null ? string.Empty : $" Published: {document.PublishedDate}";
        var summary = $"{document.ReportTitle} for {ProfileTitleHtmlFormatter.ToPlainText(document.ProfileTitle)} v{document.VersionLabel}{published}";

        return new PdfRenderOptions
        {
            HeaderHtml = ProfileReportLayout.BuildHeaderHtml(document.ReportTitle, document.ProfileTitle),
            FooterHtml =
                "<div style=\"width:100%;font-family:Arial,Helvetica,sans-serif;font-size:8px;text-align:center;\">" +
                $"{System.Net.WebUtility.HtmlEncode(summary)}<br/>{System.Net.WebUtility.HtmlEncode(ContributionsReportLayout.QualityStatementText)}<br/>" +
                "Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>",
            MarginTopMm = 30,
            MarginBottomMm = 28,
            GenerateOutline = true,
            PreferCssPageSize = true
        };
    }

    private static bool IsProfiledSpecies(Guid speciesId, ProfilePrioritisationScoresDto scores) =>
        scores.CategoryScores.FirstOrDefault(score => score.SpeciesId == speciesId)?.IsProfiledSpecies == true;
}
