using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;

namespace CDC.Web.Tests.Infrastructure;

public class SprGuidanceReportLayoutTests
{
    [Fact]
    public void Build_PrintsTheTitleAndTheSixStandardSections_WithTheWorkedExampleInTheFourth()
    {
        var report = SprGuidanceReportLayout.Build(GuidanceTestContent.Build());

        Assert.Equal("Guidance for questions in the summary prioritisation report (SPR)", report.Title);
        Assert.Equal(
        [
            "D2R2 Scoring: Overall Approach",
            "How A D2R2 Score Is Generated",
            "How The D2R2 Scoring Criteria Are Weighted And Normalised",
            "Example of the D2R2 Approach to Scoring and Weighting",
            "Why D2R2 Disease Scores Are Used To Create Rankings And How To Interpret The Rankings",
            "What Do D2R2 Scores Represent / Why Don't All Scores Appear Intuitive?"
        ], report.StandardSections.Select(section => section.Title));
        Assert.Equal([false, false, false, true, false, false], report.StandardSections.Select(section => section.HasExample));
    }

    [Fact]
    public void Build_OpensWithTheOverallApproach_ListingTheFourReasonsForGovernmentIntervention()
    {
        var overall = SprGuidanceReportLayout.Build(GuidanceTestContent.Build()).StandardSections[0].Before;

        Assert.Equal([false, true, true, true, true, false], overall.Select(text => text.IsBullet));
        Assert.StartsWith("D2R2 is a simple categorical model.", overall[0].Text, StringComparison.Ordinal);
        Assert.EndsWith("These are:", overall[0].Text, StringComparison.Ordinal);
        Assert.Equal("'to protect public health' (colour-coded pink on D2R2 outputs)", overall[1].Text);
        Assert.StartsWith("The fifth area for which a score is generated is termed the post-mitigation risk (PMR).", overall[5].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", overall[5].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_PrintsTheWorkedExample_ThenItsExplanationAndPercentages()
    {
        var report = SprGuidanceReportLayout.Build(GuidanceTestContent.Build());
        var example = report.StandardSections[3];

        Assert.Equal(7, SprGuidanceReportLayout.ExampleHeadings.Count);
        Assert.Equal("Question / Criterion Title", SprGuidanceReportLayout.ExampleHeadings[0]);
        Assert.Equal(5, report.ExampleRows.Count);
        Assert.All(report.ExampleRows, row => Assert.Equal(7, row.Cells.Count));
        Assert.Equal(["Impact on the global environment", "Negligible / Some", "0", "25", "1", "0", "25"], report.ExampleRows[1].Cells);
        Assert.Equal("875", report.ExampleTotalValue);
        Assert.Contains("divided by 875 and multiplied by 100", report.ExampleTotalNote, StringComparison.Ordinal);
        Assert.StartsWith("(using the Wider society reason", example.Before.Single().Text, StringComparison.Ordinal);
        Assert.Equal(
        [
            "Risk of unexpected government costs 29%", "Wider community social and economic impact 29%", "Environmental impact 29%", "Producer economic impact 14%"
        ], example.After.Where(text => text.IsBullet).Select(text => text.Text));
        Assert.Equal(2, example.After.Count(text => !text.IsBullet));
    }

    [Fact]
    public void Build_PrintsTheGuidanceOfTheTenQuestions_UnderTheSprHeadings()
    {
        var report = SprGuidanceReportLayout.Build(GuidanceTestContent.Build());

        Assert.Equal(
        [
            ("Summary description", "<p>Guidance 1.1</p>"),
            ("RFI 1 Protect human health", "<p>Guidance 6.14</p>"),
            ("RFI 2 Protect and promote animal welfare", "<p>Guidance 5.9</p>"),
            ("RFI 3 Protect interests of wider economy, environment & society", "<p>Guidance 7.7</p>"),
            ("RFI 4 International trade", "<p>Guidance 13.11</p>"),
            ("Post mitigation risk", "<p>Guidance 3.20</p>"),
            ("Public perception", "<p>Guidance 15.1</p>"),
            ("Summary of central government veterinary expenditure", "<p>Guidance 7.8</p>"),
            ("Uncertainty", "<p>Guidance 16.1</p>"),
            ("Legislation", "<p>Guidance 8.4</p>")
        ], report.Questions.Select(question => (question.Heading, question.Guidance)));
    }

    [Fact]
    public void Build_PrintsEachCategoryWithItsCriteria_ColouredAsOnTheSummaryPrioritisationReport()
    {
        var report = SprGuidanceReportLayout.Build(GuidanceTestContent.Build());

        Assert.Equal("Detailed criteria scores", report.CriteriaTitle);
        Assert.Equal(["Public health", "Post mitigation risk"], report.Categories.Select(category => category.Heading));
        var publicHealth = report.Categories[0];
        Assert.Equal("rgb(255,153,204)", publicHealth.BackgroundColour);
        Assert.Equal("rgb(0,0,0)", publicHealth.ForegroundColour);
        Assert.Equal(GuidanceReportLayout.EndemicOrExoticNote, publicHealth.Note);
        Assert.Equal(["Zoonosis", "<i>Endemic</i> or exotic"], publicHealth.Criteria.Select(criterion => criterion.Name));
        Assert.Equal(["<p>Guidance 6.14</p>"], publicHealth.Criteria[0].Guidance);
        var pmr = report.Categories[1];
        Assert.Equal("rgb(0,0,0)", pmr.BackgroundColour);
        Assert.Equal("rgb(255,255,255)", pmr.ForegroundColour);
        Assert.Null(pmr.Note);
        Assert.Equal(["<p>Guidance 3.20</p>"], pmr.Criteria.Single().Guidance);
    }

    [Fact]
    public void Build_FollowsTheEndemicQuestionsGuidanceWithTheExoticQuestions()
    {
        var criterion = SprGuidanceReportLayout.Build(GuidanceTestContent.Build()).Categories[0].Criteria[1];

        Assert.Equal(["<p>Guidance 2.2</p>", "<p>Guidance 2.4</p>"], criterion.Guidance);
    }

    [Theory]
    [InlineData("International trade", "rgb(255,255,189)", "rgb(0,0,0)")]
    [InlineData("WIDER SOCIETY", "rgb(153,204,255)", "rgb(0,0,0)")]
    [InlineData("Welfare", "rgb(193,255,193)", "rgb(0,0,0)")]
    [InlineData("Public health", "rgb(255,153,204)", "rgb(0,0,0)")]
    [InlineData("Post Mitigation Risk", "rgb(0,0,0)", "rgb(255,255,255)")]
    [InlineData("Anything else", "rgb(0,163,59)", "rgb(0,0,0)")]
    public void Colours_AreTheLegacyOnesForEachCategory(string category, string background, string foreground)
    {
        Assert.Equal(background, SprGuidanceReportLayout.BackgroundColourOf(category));
        Assert.Equal(foreground, SprGuidanceReportLayout.ForegroundColourOf(category));
    }

    [Fact]
    public void Build_Throws_WhenACriterionsQuestionIsNotInTheContent()
    {
        var missing = Guid.NewGuid();
        var content = GuidanceTestContent.Build(
        [
            new PrioritisationGuidanceCategoryDto
            {
                Id = Guid.NewGuid(),
                DisplayName = "Welfare",
                Criteria = [new PrioritisationGuidanceCriterionDto { ProfileQuestionId = missing, CriterionCode = "W01", CriterionName = "Lost" }]
            }
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() => SprGuidanceReportLayout.Build(content));

        Assert.Equal($"Could not find profile question with ID '{missing}'.", exception.Message);
    }

    [Fact]
    public void Build_Throws_WhenOneOfTheTenQuestionsIsMissing()
    {
        var content = new ProfileGuidanceContentDto { Sections = [new ProfileGuidanceSectionDto { SectionNumber = 1, Questions = [] }] };

        Assert.Throws<InvalidOperationException>(() => SprGuidanceReportLayout.Build(content));
    }
}
