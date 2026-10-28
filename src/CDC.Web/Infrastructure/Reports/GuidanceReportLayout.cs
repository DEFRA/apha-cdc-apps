using System.Globalization;
using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Lays out the full profile, summary profile and Q&amp;A sheet guidance reports for printing, matching the legacy
/// <c>ProfileGuidanceReportPdf</c>, <c>SummaryProfileGuidanceReportPdf</c> and <c>QAGuidanceReportPdf</c>. The summary
/// profile and Q&amp;A sheet reports print a fixed list of questions, which must match what the summary profile asks.
/// </summary>
public static class GuidanceReportLayout
{
    /// <summary>The title of the full profile guidance report.</summary>
    public const string FullTitle = "Full profile guidance report";

    /// <summary>The title of the summary profile guidance report.</summary>
    public const string SummaryProfileTitle = "Summary profile guidance report";

    /// <summary>The title of the Q&amp;A sheet guidance report.</summary>
    public const string QaTitle = "Q&A sheet guidance report";

    /// <summary>The title of the summary prioritisation report guidance report.</summary>
    public const string SprTitle = "Guidance for questions in the summary prioritisation report (SPR)";

    /// <summary>The line the legacy summary profile report prints in the section whose first question depends on the disease being endemic or exotic.</summary>
    public const string EndemicOrExoticNote =
        "The first question in this section is dependent on if a disease is endemic or exotic to Great Britain. The guidance to be followed if answering for an endemic disease is given first.";

    // The legacy file name ended "_{Now}", which became "dd_MM_yyyy_HH_mm_ss" once the date and time separators were replaced.
    private const string FileNameTimestampFormat = "dd_MM_yyyy_HH_mm_ss";

    private const string FooterDateFormat = "dd MMM yyyy";

    private static readonly (string Heading, (int Section, int Question, string Number)[] Questions)[] SummaryProfileBlocks =
    [
        ("1. Description", [(1, 1, "1.1"), (1, 3, "1.2")]),
        ("2. Rationale for government intervention", [(6, 14, "2.1"), (5, 9, "2.2"), (7, 7, "2.3"), (13, 11, "2.4")]),
        ("3. Legislation and other mandates", [(8, 4, "3.1")]),
        ("4. Geographic distribution", [(2, 1, "4.1"), (2, 7, "4.2")]),
        ("5. Risk of introduction/spread", [(2, 2, "5.1 (1)"), (2, 4, "5.1 (2)")]),
        ("6. Human health implications", [(6, 1, "6.1"), (6, 5, "6.2"), (6, 3, "6.3")]),
        ("7. GB disease control strategy", [(9, 1, "7.1")]),
        ("8. Current surveillance", [(12, 2, "8.1")]),
        ("9. Costs", [(7, 6, "9.1"), (7, 8, "9.2")]),
        ("10. Compensation", [(9, 11, "10.1")])
    ];

    // The Q&A sheet's numbering, in print order. A null section marks a line of explanation, named by the question it comes before or after.
    private static readonly (string Number, int Section, int Question)[] QaQuestions =
    [
        ("1", 1, 1), ("2", 6, 14), ("3", 6, 1), ("note-3", 0, 0), ("4", 6, 5), ("5", 6, 3), ("6", 6, 6), ("7", 3, 13), ("8", 3, 17),
        ("9", 1, 1), ("9a", 1, 3), ("9b", 1, 4), ("10", 3, 7), ("11", 3, 8), ("12", 9, 8), ("13", 3, 15), ("14", 8, 1), ("15", 8, 2),
        ("16", 2, 1), ("note-16", 0, 0), ("16 1a", 5, 8), ("16 1b", 2, 6), ("16 2a", 2, 3), ("16 2b", 2, 4), ("17", 2, 7), ("18", 10, 6),
        ("note-19", 0, 0), ("19", 12, 1), ("20", 9, 1), ("21", 9, 2), ("22", 5, 9), ("23", 13, 11)
    ];

    private static readonly Dictionary<string, string> QaNotes = new(StringComparer.Ordinal)
    {
        ["note-3"] = "For a disease or issue that does not affect people questions 4, 5 and 6 will not have an answer provided in the Q&A sheet. Under each of these questions the statement ‘This question is not relevant’ will appear.",
        ["note-16"] = "Depending on the answer selected for question 16, two alternate pairs of additional sub-questions appear in the Q&A sheet. The guidance for these is provided, firstly for the two questions that apply if a disease is endemic and then for the pair of questions that will be included if the disease is exotic to Great Britain.",
        ["note-19"] = "Regarding question 19 (Current surveillance activities). The Q&A sheet provides detailed information about all surveillance pertinent to the profiled disease or issue. If preferred a short summary can be found in the Summary profile."
    };

    /// <summary>Gets the title the legacy report class gave a report type.</summary>
    /// <param name="type">The report.</param>
    /// <returns>The title printed on the report and in its footer.</returns>
    public static string TitleOf(GuidanceReportType type) => type switch
    {
        GuidanceReportType.All => FullTitle,
        GuidanceReportType.SummaryProfile => SummaryProfileTitle,
        GuidanceReportType.SummaryPrioritisationReport => SprTitle,
        GuidanceReportType.QAGuidanceReport => QaTitle,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown guidance report type.")
    };

    /// <summary>Lays out the full profile, summary profile or Q&amp;A sheet guidance report.</summary>
    /// <param name="type">The report; not <see cref="GuidanceReportType.SummaryPrioritisationReport"/>, which has its own layout.</param>
    /// <param name="content">The sections, questions and guidance.</param>
    /// <returns>The laid-out report.</returns>
    /// <exception cref="InvalidOperationException">A question the report prints is not in <paramref name="content"/>.</exception>
    public static GuidanceReportViewModel Build(GuidanceReportType type, ProfileGuidanceContentDto content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return type switch
        {
            GuidanceReportType.All => new GuidanceReportViewModel(FullTitle, BuildFull(content)),
            GuidanceReportType.SummaryProfile => new GuidanceReportViewModel(SummaryProfileTitle, BuildSummaryProfile(content)),
            GuidanceReportType.QAGuidanceReport => new GuidanceReportViewModel(QaTitle, [BuildQa(content)]),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This report has its own layout.")
        };
    }

    /// <summary>Finds a question by its section and question number, as the legacy <c>ProfileQuestionData</c> did.</summary>
    /// <param name="content">The sections, questions and guidance.</param>
    /// <param name="sectionNumber">The section's number.</param>
    /// <param name="questionNumber">The question's number within the section.</param>
    /// <returns>The question.</returns>
    /// <exception cref="InvalidOperationException">There is no such section or question.</exception>
    public static ProfileGuidanceQuestionDto FindQuestion(ProfileGuidanceContentDto content, int sectionNumber, int questionNumber)
    {
        ArgumentNullException.ThrowIfNull(content);

        var section = content.Sections.SingleOrDefault(candidate => candidate.SectionNumber == sectionNumber)
            ?? throw new InvalidOperationException($"Could not find section with number '{sectionNumber.ToString(CultureInfo.InvariantCulture)}'.");

        return section.Questions.SingleOrDefault(candidate => candidate.QuestionNumber == questionNumber)
            ?? throw new InvalidOperationException(
                $"Could not find question with number '{questionNumber.ToString(CultureInfo.InvariantCulture)}' in section with number '{sectionNumber.ToString(CultureInfo.InvariantCulture)}'.");
    }

    /// <summary>
    /// Builds the page footer: the report's title, the page number and the date over a rule, then the quality statement.
    /// </summary>
    /// <param name="title">The report's title.</param>
    /// <param name="producedOn">When the report was produced.</param>
    /// <returns>The render options.</returns>
    public static PdfRenderOptions BuildPdfOptions(string title, DateTimeOffset producedOn) => new()
    {
        // border-box keeps the right-aligned date inside the page, rather than a margin's width past its edge.
        FooterHtml =
            "<div style=\"width:100%;box-sizing:border-box;padding:0 12.7mm;font-family:Arial,Helvetica,sans-serif;font-size:8px;\">" +
            "<table style=\"width:100%;border-collapse:collapse;border-top:1px solid #000;\"><tr>" +
            $"<td style=\"width:40%;padding-top:3px;text-align:left;\">{WebUtility.HtmlEncode(title)}.</td>" +
            "<td style=\"width:20%;padding-top:3px;text-align:center;\">Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></td>" +
            $"<td style=\"width:40%;padding-top:3px;text-align:right;\">{producedOn.ToString(FooterDateFormat, CultureInfo.InvariantCulture)}</td></tr>" +
            $"<tr><td colspan=\"3\" style=\"padding-top:2px;\">{WebUtility.HtmlEncode(ContributionsReportLayout.QualityStatementText)}</td></tr></table></div>",
        MarginTopMm = 15,
        MarginBottomMm = 25,
        GenerateOutline = true
    };

    /// <summary>Builds the file name offered for download: the legacy report name and the time it was generated.</summary>
    /// <param name="type">The report.</param>
    /// <param name="generatedAt">When the report was generated.</param>
    /// <returns>A file name ending ".pdf".</returns>
    public static string BuildFileName(GuidanceReportType type, DateTimeOffset generatedAt)
    {
        var name = type switch
        {
            GuidanceReportType.All => "Profile Guidance",
            GuidanceReportType.SummaryProfile => "Summary Profile Guidance",
            GuidanceReportType.SummaryPrioritisationReport => "Summary Prioritisation Report Guidance",
            GuidanceReportType.QAGuidanceReport => "Q&A Guidance Report",
            _ => "Guidance"
        };

        return PdfFileName.Build($"{name}_{generatedAt.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture)}");
    }

    private static List<GuidanceReportBlockView> BuildFull(ProfileGuidanceContentDto content) =>
    [
        .. content.Sections.OrderBy(section => section.SectionNumber).Select(section => new GuidanceReportBlockView(
            $"{section.SectionNumber.ToString(CultureInfo.InvariantCulture)} - {section.Name}",
            [.. section.Questions.OrderBy(question => question.QuestionNumber).Select(question =>
                Question(question.QuestionNumber.ToString(CultureInfo.InvariantCulture), question.Name, question.UserGuidance))]))
    ];

    private static List<GuidanceReportBlockView> BuildSummaryProfile(ProfileGuidanceContentDto content)
    {
        var blocks = new List<GuidanceReportBlockView>();

        foreach (var (heading, questions) in SummaryProfileBlocks)
        {
            var rows = new List<GuidanceReportRowView>();

            // Section 5's first question depends on the disease being endemic or exotic, and the guidance for both is printed
            // because the report is not in the context of one profile.
            if (heading.StartsWith("5.", StringComparison.Ordinal))
            {
                rows.Add(new GuidanceReportRowView(GuidanceRowKind.Note, string.Empty, string.Empty, EndemicOrExoticNote));
            }

            rows.AddRange(questions.Select(entry =>
            {
                var question = FindQuestion(content, entry.Section, entry.Question);

                return Question(entry.Number, question.Name, question.UserGuidance);
            }));

            blocks.Add(new GuidanceReportBlockView(heading, rows));
        }

        return blocks;
    }

    private static GuidanceReportBlockView BuildQa(ProfileGuidanceContentDto content)
    {
        var rows = new List<GuidanceReportRowView>();

        foreach (var (number, section, questionNumber) in QaQuestions)
        {
            if (QaNotes.TryGetValue(number, out var note))
            {
                rows.Add(new GuidanceReportRowView(GuidanceRowKind.HighlightedNote, string.Empty, string.Empty, note));

                continue;
            }

            var question = FindQuestion(content, section, questionNumber);

            // Question 9 repeats the first question as the heading of the animals that are affected, so it has no guidance of its own.
            rows.Add(Question(number, question.NonTechnicalName, number == "9" ? string.Empty : question.UserGuidance));
        }

        return new GuidanceReportBlockView(null, rows);
    }

    private static GuidanceReportRowView Question(string number, string name, string guidance) =>
        new(GuidanceRowKind.Question, number, name, guidance);
}
