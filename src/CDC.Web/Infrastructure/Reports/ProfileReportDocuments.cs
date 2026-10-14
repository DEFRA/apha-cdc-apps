using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Lays out the full profile, summary profile and Q&amp;A brief reports from a profile version's
/// content, following the question maps of the legacy <c>FullProfilePdf</c>, <c>SummaryProfilePdf</c>
/// and <c>QABriefPdf</c>.
/// </summary>
public static class ProfileReportDocuments
{
    /// <summary>The internal report name of the full profile, which is only ever printed.</summary>
    public const string FullProfileReportName = "FullProfileGUID";

    /// <summary>The internal report name of the summary profile.</summary>
    public const string SummaryProfileReportName = "SummaryProfileGUID";

    /// <summary>The internal report name of the Q&amp;A brief.</summary>
    public const string QABriefReportName = "QABriefGUID";

    private const string FullProfileTitle = "Full profile";
    private const string SummaryProfileTitle = "Summary profile";
    private const string QABriefTitle = "Question and answer brief";

    /// <summary>Lays out the full profile: one section per profile section, each on its own page.</summary>
    /// <param name="content">The profile version's report content.</param>
    /// <param name="profileSectionId">The one section to print, or <see cref="Guid.Empty"/> for every section.</param>
    /// <param name="references">The landscape reference tables.</param>
    /// <returns>The document. A single-section print has no front page.</returns>
    public static ProfileReportDocumentViewModel BuildFullProfile(
        ProfileReportContentDto content,
        Guid profileSectionId,
        IReadOnlyList<ReportReferenceSectionView> references)
    {
        var sections = content.Sections
            .Where(section => profileSectionId == Guid.Empty || section.Id == profileSectionId)
            .Select(section => new ReportSectionView(
                $"{section.SectionNumber}. {ProfileTitleHtmlFormatter.ToPlainText(section.Name)}",
                $"section-{section.SectionNumber}",
                string.IsNullOrWhiteSpace(section.Note) ? null : section.Note,
                StartsOnNewPage: true,
                [
                    .. section.Questions.Select(question => ProfileReportLayout.Question(
                        $"{section.SectionNumber}.{question.QuestionNumber}",
                        question.Name,
                        question))
                ]))
            .ToList();

        return ProfileReportLayout.CreateDocument(
            FullProfileTitle,
            content,
            sections,
            showFrontPage: profileSectionId == Guid.Empty,
            references);
    }

    /// <summary>Lays out the summary profile: ten headings over the legacy renumbered selection of questions.</summary>
    /// <param name="content">The profile version's report content.</param>
    /// <returns>The document.</returns>
    public static ProfileReportDocumentViewModel BuildSummaryProfile(ProfileReportContentDto content)
    {
        ReportQuestionView? Renumbered(int section, int question, string number) =>
            ProfileReportLayout.FindQuestion(content, section, question) is { } found
                ? ProfileReportLayout.Question(number, found.Name, found)
                : null;

        ReportSectionView Section(string heading, int index, params ReportQuestionView?[] questions) =>
            new(heading, $"summary-{index}", null, StartsOnNewPage: false, [.. questions.OfType<ReportQuestionView>()]);

        // Legacy BuildRenumberedControlledQuestionTable(2, 1, "yes", 2, 2, 2, 4, "5.1").
        var controllingAnswer = ProfileReportLayout.FirstAnswerText(ProfileReportLayout.FindQuestion(content, 2, 1));
        var riskQuestion = string.Equals(controllingAnswer, "yes", StringComparison.OrdinalIgnoreCase)
            ? Renumbered(2, 2, "5.1")
            : Renumbered(2, 4, "5.1");

        List<ReportSectionView> sections =
        [
            Section("1. Description", 1, Renumbered(1, 1, "1.1"), Renumbered(1, 3, "1.2")),
            Section("2. Rationale for government intervention", 2, Renumbered(6, 14, "2.1"), Renumbered(5, 9, "2.2"), Renumbered(7, 7, "2.3"), Renumbered(13, 11, "2.4")),
            Section("3. Legislation and other mandates", 3, Renumbered(8, 4, "3.1")),
            Section("4. Geographic distribution", 4, Renumbered(2, 1, "4.1"), Renumbered(2, 7, "4.2")),
            Section("5. Risk of introduction/spread", 5, riskQuestion),
            Section("6. Human health implications", 6, Renumbered(6, 1, "6.1"), Renumbered(6, 5, "6.2"), Renumbered(6, 3, "6.3")),
            Section("7. GB disease control strategy", 7, Renumbered(9, 1, "7.1")),
            Section("8. Current surveillance", 8, Renumbered(12, 2, "8.1")),
            Section("9. Costs", 9, Renumbered(7, 6, "9.1"), Renumbered(7, 8, "9.2")),
            Section("10. Compensation", 10, Renumbered(9, 11, "10.1"))
        ];

        return ProfileReportLayout.CreateDocument(SummaryProfileTitle, content, sections);
    }

    /// <summary>Lays out the Q&amp;A brief: the legacy numbered selection of questions under their plain-language names.</summary>
    /// <param name="content">The profile version's report content.</param>
    /// <returns>The document.</returns>
    public static ProfileReportDocumentViewModel BuildQABrief(ProfileReportContentDto content)
    {
        ReportQuestionView? NonTechnical(int section, int question, string number, bool showName = true, int level = 0)
        {
            var found = ProfileReportLayout.FindQuestion(content, section, question);

            if (found is null)
            {
                return null;
            }

            var name = showName ? found.Name : null;

            return new ReportQuestionView($"{number} {ProfileTitleHtmlFormatter.ToPlainText(found.NonTechnicalName)}", name, found, level);
        }

        // Legacy BuildQuestion16: the answer to 2.1 picks which pair of follow-up questions is printed.
        var isPresent = !string.Equals(ProfileReportLayout.FirstAnswerText(ProfileReportLayout.FindQuestion(content, 2, 1)), "No", StringComparison.Ordinal);

        ReportQuestionView?[] questions =
        [
            NonTechnical(1, 1, "1"),
            NonTechnical(6, 14, "2"),
            NonTechnical(6, 1, "3"),
            NonTechnical(6, 5, "4"),
            NonTechnical(6, 3, "5"),
            NonTechnical(6, 6, "6"),
            NonTechnical(3, 13, "7"),
            NonTechnical(3, 17, "8"),
            new ReportQuestionView("9 What animals are affected?", null, null, 0),
            NonTechnical(1, 3, "9a", showName: false, level: 1),
            NonTechnical(1, 4, "9b", showName: false, level: 1),
            NonTechnical(3, 7, "10"),
            NonTechnical(3, 8, "11"),
            NonTechnical(9, 8, "12"),
            NonTechnical(3, 15, "13"),
            NonTechnical(8, 1, "14"),
            NonTechnical(8, 2, "15"),
            NonTechnical(2, 1, "16"),
            isPresent ? NonTechnical(5, 8, "16.1a", level: 1) : NonTechnical(2, 3, "16.2a", level: 1),
            isPresent ? NonTechnical(2, 6, "16.1b", level: 1) : NonTechnical(2, 4, "16.2b", level: 1),
            NonTechnical(2, 7, "17"),
            NonTechnical(10, 6, "18"),
            NonTechnical(12, 1, "19"),
            NonTechnical(9, 1, "20"),
            NonTechnical(9, 2, "21"),
            NonTechnical(5, 9, "22"),
            NonTechnical(13, 11, "23")
        ];

        ReportSectionView[] sections = [new(null, "qa-brief", null, StartsOnNewPage: false, [.. questions.OfType<ReportQuestionView>()])];

        return ProfileReportLayout.CreateDocument(QABriefTitle, content, sections);
    }
}
