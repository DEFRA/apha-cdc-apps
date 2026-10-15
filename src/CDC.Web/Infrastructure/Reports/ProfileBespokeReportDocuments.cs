using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Lays out the bespoke report from a profile version's content and the sections, questions and
/// guidance ticked on the "Create bespoke report" tree, following the legacy
/// <c>ProfileVersionBespokeReportPdf</c>: each ticked section on its own page with its note, then
/// each ticked question's answers and each ticked guidance table in question order.
/// </summary>
public static class ProfileBespokeReportDocuments
{
    /// <summary>The title used when no template is selected.</summary>
    public const string DefaultTitle = "Bespoke Profile Report";

    /// <summary>Finds the questions whose guidance the report prints: ticked guidance in ticked sections.</summary>
    /// <param name="content">The profile version's report content.</param>
    /// <param name="selection">What was ticked.</param>
    /// <returns>The question identifiers.</returns>
    public static IReadOnlyList<Guid> GuidanceQuestionIds(ProfileReportContentDto content, BespokeReportSelection selection) =>
    [
        .. content.Sections
            .Where(section => selection.Sections.Contains(section.Id))
            .SelectMany(section => section.Questions)
            .Select(question => question.Id)
            .Where(selection.Guidance.Contains)
            .Distinct()
    ];

    /// <summary>Lays the report out.</summary>
    /// <param name="content">The profile version's report content.</param>
    /// <param name="selection">What was ticked.</param>
    /// <param name="guidance">The guidance text of each question whose guidance is printed.</param>
    /// <param name="templateTitle">The selected template's title, or <see langword="null"/> when none is selected.</param>
    /// <returns>The document.</returns>
    public static ProfileReportDocumentViewModel Build(
        ProfileReportContentDto content,
        BespokeReportSelection selection,
        IReadOnlyDictionary<Guid, string> guidance,
        string? templateTitle)
    {
        var sections = content.Sections
            .Where(section => selection.Sections.Contains(section.Id))
            .Select(section => new ReportSectionView(
                $"{section.SectionNumber}. {ProfileTitleHtmlFormatter.ToPlainText(section.Name)}",
                $"section-{section.SectionNumber}",
                string.IsNullOrWhiteSpace(section.Note) ? null : section.Note,
                StartsOnNewPage: true,
                [.. section.Questions.SelectMany(question => QuestionViews(section, question, selection, guidance))]))
            .ToList();

        return ProfileReportLayout.CreateDocument(string.IsNullOrWhiteSpace(templateTitle) ? DefaultTitle : templateTitle, content, sections);
    }

    private static IEnumerable<ReportQuestionView> QuestionViews(
        ProfileReportSectionDto section,
        ProfileReportQuestionDto question,
        BespokeReportSelection selection,
        IReadOnlyDictionary<Guid, string> guidance)
    {
        var number = $"{section.SectionNumber}.{question.QuestionNumber}";

        if (selection.Questions.Contains(question.Id))
        {
            yield return ProfileReportLayout.Question(number, question.Name, question);
        }

        if (selection.Guidance.Contains(question.Id))
        {
            yield return new ReportQuestionView(
                $"{number} : {ProfileTitleHtmlFormatter.ToPlainText(question.Name)} (Guidance)",
                AdditionalInfo: null,
                Question: null,
                GuidanceHtml: guidance.GetValueOrDefault(question.Id, string.Empty));
        }
    }
}
