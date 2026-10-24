using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;

namespace CDC.Web.Tests.Infrastructure;

public class GuidanceReportLayoutTests
{
    private static readonly DateTimeOffset ProducedAt = new(2027, 3, 4, 14, 5, 6, TimeSpan.Zero);

    [Fact]
    public void Build_FullReport_HasAHeadingPerSection_InOrder_WithEveryQuestionInOrder()
    {
        var report = GuidanceReportLayout.Build(GuidanceReportType.All, GuidanceTestContent.Build());

        Assert.Equal("Full profile guidance report", report.Title);
        Assert.Equal(GuidanceTestContent.SectionCount, report.Blocks.Count);
        Assert.Equal("1 - Section 1", report.Blocks[0].Heading);
        Assert.Equal("16 - Section 16", report.Blocks[^1].Heading);
        var first = report.Blocks[0];
        Assert.Equal(GuidanceTestContent.QuestionsPerSection, first.Rows.Count);
        Assert.Equal(Enumerable.Range(1, GuidanceTestContent.QuestionsPerSection).Select(number => number.ToString(CultureInfo.InvariantCulture)), first.Rows.Select(row => row.Number));
        Assert.Equal(new GuidanceReportRowView(GuidanceRowKind.Question, "3", "Q1.3 <i>name</i>", "<p>Guidance 1.3</p>"), first.Rows[2]);
    }

    [Fact]
    public void Build_SummaryProfile_PrintsTheTenHeadings_WithTheLegacyQuestionsAndNumbers()
    {
        var report = GuidanceReportLayout.Build(GuidanceReportType.SummaryProfile, GuidanceTestContent.Build());

        Assert.Equal("Summary profile guidance report", report.Title);
        Assert.Equal(
        [
            "1. Description", "2. Rationale for government intervention", "3. Legislation and other mandates", "4. Geographic distribution",
            "5. Risk of introduction/spread", "6. Human health implications", "7. GB disease control strategy", "8. Current surveillance",
            "9. Costs", "10. Compensation"
        ], report.Blocks.Select(block => block.Heading));

        string Rows(int block) => string.Join("; ", report.Blocks[block].Rows.Select(row => $"{row.Number}={row.Name}"));

        Assert.Equal("1.1=Q1.1 <i>name</i>; 1.2=Q1.3 <i>name</i>", Rows(0));
        Assert.Equal("2.1=Q6.14 <i>name</i>; 2.2=Q5.9 <i>name</i>; 2.3=Q7.7 <i>name</i>; 2.4=Q13.11 <i>name</i>", Rows(1));
        Assert.Equal("3.1=Q8.4 <i>name</i>", Rows(2));
        Assert.Equal("4.1=Q2.1 <i>name</i>; 4.2=Q2.7 <i>name</i>", Rows(3));
        Assert.Equal("6.1=Q6.1 <i>name</i>; 6.2=Q6.5 <i>name</i>; 6.3=Q6.3 <i>name</i>", Rows(5));
        Assert.Equal("7.1=Q9.1 <i>name</i>", Rows(6));
        Assert.Equal("8.1=Q12.2 <i>name</i>", Rows(7));
        Assert.Equal("9.1=Q7.6 <i>name</i>; 9.2=Q7.8 <i>name</i>", Rows(8));
        Assert.Equal("10.1=Q9.11 <i>name</i>", Rows(9));
    }

    [Fact]
    public void Build_SummaryProfile_ExplainsTheEndemicOrExoticQuestion_BeforeBothOfItsGuidances()
    {
        var rows = GuidanceReportLayout.Build(GuidanceReportType.SummaryProfile, GuidanceTestContent.Build()).Blocks[4].Rows;

        Assert.Equal(GuidanceRowKind.Note, rows[0].Kind);
        Assert.Equal(GuidanceReportLayout.EndemicOrExoticNote, rows[0].Guidance);
        Assert.Equal(["5.1 (1)", "5.1 (2)"], rows.Skip(1).Select(row => row.Number));
        Assert.Equal("<p>Guidance 2.2</p>", rows[1].Guidance);
        Assert.Equal("<p>Guidance 2.4</p>", rows[2].Guidance);
    }

    [Fact]
    public void Build_QaSheet_PrintsOneTableWithNoHeading_UsingTheNonTechnicalTitles()
    {
        var report = GuidanceReportLayout.Build(GuidanceReportType.QAGuidanceReport, GuidanceTestContent.Build());

        Assert.Equal("Q&A sheet guidance report", report.Title);
        var block = Assert.Single(report.Blocks);
        Assert.Null(block.Heading);
        var questions = block.Rows.Where(row => row.Kind == GuidanceRowKind.Question).ToList();
        Assert.Equal(
        [
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "9a", "9b", "10", "11", "12", "13", "14", "15",
            "16", "16 1a", "16 1b", "16 2a", "16 2b", "17", "18", "19", "20", "21", "22", "23"
        ], questions.Select(row => row.Number));
        Assert.Equal("Plain 1.1", questions[0].Name);
        Assert.Equal("<p>Guidance 1.1</p>", questions[0].Guidance);
        Assert.Equal("Plain 6.14", questions[1].Name);
        Assert.Equal("Plain 13.11", questions[^1].Name);
    }

    [Fact]
    public void Build_QaSheet_GivesQuestionNineNoGuidance_AndPrintsTheThreeNotesInRed_BeforeTheirQuestions()
    {
        var rows = GuidanceReportLayout.Build(GuidanceReportType.QAGuidanceReport, GuidanceTestContent.Build()).Blocks[0].Rows;

        Assert.Equal(string.Empty, rows.Single(row => row.Number == "9").Guidance);
        Assert.Equal("<p>Guidance 1.3</p>", rows.Single(row => row.Number == "9a").Guidance);

        var notes = rows.Select((row, index) => (row, index)).Where(entry => entry.row.Kind == GuidanceRowKind.HighlightedNote).ToList();
        Assert.Equal(3, notes.Count);
        Assert.StartsWith("For a disease or issue that does not affect people", notes[0].row.Guidance, StringComparison.Ordinal);
        Assert.StartsWith("Depending on the answer selected for question 16", notes[1].row.Guidance, StringComparison.Ordinal);
        Assert.StartsWith("Regarding question 19", notes[2].row.Guidance, StringComparison.Ordinal);
        Assert.Equal("3", rows[notes[0].index - 1].Number);
        Assert.Equal("16", rows[notes[1].index - 1].Number);
        Assert.Equal("19", rows[notes[2].index + 1].Number);
        Assert.All(notes, entry => Assert.Equal(string.Empty, entry.row.Number));
    }

    [Fact]
    public void Build_Throws_ForTheSummaryPrioritisationReport_WhichHasItsOwnLayout() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GuidanceReportLayout.Build(GuidanceReportType.SummaryPrioritisationReport, GuidanceTestContent.Build()));

    [Fact]
    public void FindQuestion_ThrowsWithTheLegacyMessages_WhenTheSectionOrQuestionIsMissing()
    {
        var content = GuidanceTestContent.Build();

        var noSection = Assert.Throws<InvalidOperationException>(() => GuidanceReportLayout.FindQuestion(content, 99, 1));
        var noQuestion = Assert.Throws<InvalidOperationException>(() => GuidanceReportLayout.FindQuestion(content, 1, 99));

        Assert.Equal("Could not find section with number '99'.", noSection.Message);
        Assert.Equal("Could not find question with number '99' in section with number '1'.", noQuestion.Message);
    }

    [Theory]
    [InlineData(GuidanceReportType.SummaryProfile)]
    [InlineData(GuidanceReportType.QAGuidanceReport)]
    public void Build_Throws_WhenAQuestionTheReportPrintsIsMissing(GuidanceReportType type)
    {
        var content = new ProfileGuidanceContentDto { Sections = [new ProfileGuidanceSectionDto { SectionNumber = 1, Name = "Only", Questions = [] }] };

        Assert.Throws<InvalidOperationException>(() => GuidanceReportLayout.Build(type, content));
    }

    [Fact]
    public void Build_FullReport_PrintsWhateverExists_SoASectionWithNoQuestionsIsNotAnError()
    {
        var content = new ProfileGuidanceContentDto { Sections = [new ProfileGuidanceSectionDto { SectionNumber = 1, Name = "Only", Questions = [] }] };

        var block = Assert.Single(GuidanceReportLayout.Build(GuidanceReportType.All, content).Blocks);

        Assert.Equal("1 - Only", block.Heading);
        Assert.Empty(block.Rows);
    }

    [Theory]
    [InlineData(GuidanceReportType.All, "Full profile guidance report")]
    [InlineData(GuidanceReportType.SummaryProfile, "Summary profile guidance report")]
    [InlineData(GuidanceReportType.SummaryPrioritisationReport, "Guidance for questions in the summary prioritisation report (SPR)")]
    [InlineData(GuidanceReportType.QAGuidanceReport, "Q&A sheet guidance report")]
    public void TitleOf_IsTheLegacyReportTitle(GuidanceReportType type, string expected) =>
        Assert.Equal(expected, GuidanceReportLayout.TitleOf(type));

    [Fact]
    public void TitleOf_Throws_ForAnUnknownType() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GuidanceReportLayout.TitleOf((GuidanceReportType)99));

    [Fact]
    public void BuildPdfOptions_PutsTheEncodedTitle_ThePageNumber_TheDate_AndTheQualityStatementInTheFooter()
    {
        var options = GuidanceReportLayout.BuildPdfOptions("Q&A sheet guidance report", ProducedAt);

        var footer = options.FooterHtml!;
        Assert.Contains("Q&amp;A sheet guidance report.</td>", footer, StringComparison.Ordinal);
        Assert.Contains("Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span>", footer, StringComparison.Ordinal);
        Assert.Contains(">04 Mar 2027</td>", footer, StringComparison.Ordinal);
        Assert.Contains(ContributionsReportLayout.QualityStatementText, footer, StringComparison.Ordinal);
        Assert.Contains("box-sizing:border-box;padding:0 12.7mm", footer, StringComparison.Ordinal);
        Assert.True(options.MarginBottomMm >= 20);
        Assert.Null(options.HeaderHtml);
    }

    [Theory]
    [InlineData(GuidanceReportType.All, "Profile Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData(GuidanceReportType.SummaryProfile, "Summary Profile Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData(GuidanceReportType.SummaryPrioritisationReport, "Summary Prioritisation Report Guidance_04_03_2027_14_05_06.pdf")]
    [InlineData(GuidanceReportType.QAGuidanceReport, "Q&A Guidance Report_04_03_2027_14_05_06.pdf")]
    [InlineData((GuidanceReportType)99, "Guidance_04_03_2027_14_05_06.pdf")]
    public void BuildFileName_IsTheLegacyNameAndTheTimeItWasGenerated(GuidanceReportType type, string expected) =>
        Assert.Equal(expected, GuidanceReportLayout.BuildFileName(type, ProducedAt));
}
