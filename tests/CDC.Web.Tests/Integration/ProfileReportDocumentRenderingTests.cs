using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.Infrastructure;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Integration;

// Renders the real profile report Razor views (through the real view renderer) to check the markup
// each kind of question, answer and section produces.
public class ProfileReportDocumentRenderingTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory factory;

    public ProfileReportDocumentRenderingTests(CdcWebTestFactory factory) => this.factory = factory;

    [Fact]
    public async Task RendersTheFrontPage_Sections_QuestionTables_AndTheWarning()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(1, "Description", "Read <b>this</b>", ReportContent.Answered(1, "What is it?", "<p>An <em>animal</em> disease</p>"))
        ]);

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, []));

        Assert.Contains("<title>Full profile for Anthrax (What if) - v1.2 (Draft) Valid from 04/03/2026</title>", html);
        Assert.Contains("Full profile for: Anthrax <em>(What if)</em>", html);
        Assert.Contains("<p>Version: 1.2</p>", html);
        Assert.DoesNotContain("Published:", html);
        Assert.Contains("class=\"rpt-watermark\"", html);
        Assert.Contains("DRAFT CONTENT", html);
        Assert.Contains("<a href=\"#section-1\">1. Description</a>", html);
        Assert.Contains("The D2R2 Quality Statement can be found in the footer", html);
        Assert.Contains("rpt-section rpt-section--new-page", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">1. Description</h2>", html);
        Assert.Contains("<p class=\"rpt-section-note\">Read <b>this</b></p>", html);
        Assert.Contains("<h3>1.1 What is it?</h3>", html);
        Assert.Contains("<p class=\"rpt-label\">Answer</p>", html);
        Assert.Contains("<div class=\"rpt-text\">An <em>animal</em> disease</div>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Please Note</h2>", html);
        Assert.Contains("The content in a number of questions constitutes static information", html);
    }

    [Fact]
    public async Task RendersAPublishedVersionWithoutTheWatermark_AndWithItsPublishedDate()
    {
        var content = ReportContent.Create([], isPublished: true, showDraftWatermark: false);

        var html = await RenderAsync(ProfileReportDocuments.BuildSummaryProfile(content));

        Assert.DoesNotContain("class=\"rpt-watermark\"", html);
        Assert.Contains("<p>Published: 4 March 2026</p>", html);
    }

    [Fact]
    public async Task RendersOptionsAsTickedAndUntickedCheckboxes_AndEmptyTextAsNoData()
    {
        var question = ReportContent.Question(
            1,
            "Status?",
            groups: ReportContent.Group(fields:
            [
                ReportContent.Options("Is it endemic?", new ProfileReportOptionDto("Yes", true), new ProfileReportOptionDto("No <em>x</em>", false)),
                ReportContent.Text("Details", string.Empty)
            ]));
        var content = ReportContent.Create([ReportContent.Section(1, "S", questions: question)]);

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, []));

        Assert.Contains("<li><span class=\"rpt-checkbox rpt-checkbox--selected\"></span><span>Yes</span></li>", html);
        Assert.Contains("<li><span class=\"rpt-checkbox \"></span><span>No <em>x</em></span></li>", html);
        Assert.Contains("<p class=\"rpt-no-data\">- No data has been entered -</p>", html);
    }

    [Fact]
    public async Task RendersRepeatingRows_PerSpeciesGroups_AndNotRelevantMessages()
    {
        var repeating = ReportContent.Question(
            1,
            "Disease",
            kind: ProfileReportQuestionKind.Repeating,
            groups:
            [
                ReportContent.Group("Disease 1", fields: ReportContent.Text("Name", "First")),
                ReportContent.Group("Disease 2", fields: ReportContent.Text("Name", "Second"))
            ]);
        var noRows = ReportContent.Question(2, "None", kind: ProfileReportQuestionKind.Repeating);
        var irrelevant = ReportContent.Question(3, "Skipped", isRelevant: false);
        var perSpecies = ReportContent.Question(
            4,
            "Species",
            kind: ProfileReportQuestionKind.PerSpecies,
            groups:
            [
                ReportContent.Group("Cattle", fields: ReportContent.Text("Detail", "Moo")),
                ReportContent.Group("Sheep <em>ovis</em>", isRelevant: false)
            ]);
        var content = ReportContent.Create([ReportContent.Section(1, "S", questions: [repeating, noRows, irrelevant, perSpecies])]);

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, []));

        Assert.Contains("<div class=\"rpt-subheading\">Disease 1</div>", html);
        Assert.Contains("<div class=\"rpt-subheading\">Disease 2</div>", html);
        Assert.Contains("- No answers were provided for this question -", html);
        Assert.Contains("<p class=\"rpt-not-relevant\">- This question is not relevant -</p>", html);
        Assert.Contains("<div class=\"rpt-subheading\">Cattle</div>", html);
        Assert.Contains("<div class=\"rpt-subheading\">Sheep <em>ovis</em></div>", html);
        Assert.Equal(2, CountOccurrences(html, "- This question is not relevant -"));
    }

    [Fact]
    public async Task RendersAFieldGroupAsNestedSpeciesGroups()
    {
        var group = new ProfileReportFieldDto
        {
            Label = "Group",
            Kind = ProfileReportFieldKind.FieldGroup,
            Groups = [ReportContent.Group("Cattle", fields: ReportContent.Text("Child", "Cattle child"))]
        };
        var content = ReportContent.Create([ReportContent.Section(1, "S", questions: ReportContent.Question(1, "Q", groups: ReportContent.Group(fields: group)))]);

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, []));

        Assert.Contains("<div class=\"rpt-nested\">", html);
        Assert.Contains("<div class=\"rpt-subheading\">Cattle</div>", html);
        Assert.Contains("Cattle child", html);
    }

    [Fact]
    public async Task RendersTheQABriefWithoutHeadings_AndAHeadingOnlyRowForQuestion9()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "Description", questions: ReportContent.Answered(1, "Technical", "a", "Plain"))]);

        var html = await RenderAsync(ProfileReportDocuments.BuildQABrief(content));

        Assert.Contains("<h1 class=\"rpt-title\">Question and answer brief for:", html);
        Assert.DoesNotContain("<h2 class=\"rpt-contents-heading\">", html);
        Assert.Contains("<h3>1 Plain</h3>", html);
        Assert.Contains("<td class=\"rpt-question__info\">Technical</td>", html);
        Assert.Contains("<h3>9 What animals are affected?</h3>", html);
        Assert.Contains("rpt-question--level-0", html);
    }

    [Fact]
    public async Task RendersTheLandscapeReferenceTables()
    {
        var references = new[]
        {
            new ReportReferenceSectionView(
                "1. Description",
                [
                    new ReportReferenceTableView("Scientific paper reference", "- No scientific paper references supplied -", [new ReportReferenceRow("1.1, 2.4", "<p>Smith 2020</p>")]),
                    new ReportReferenceTableView("Legislative reference", "- No legislative references supplied -", [])
                ])
        };
        var content = ReportContent.Create([ReportContent.Section(1, "Description")]);

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, references));

        Assert.Contains("<section id=\"references\" class=\"rpt-landscape\">", html);
        Assert.Contains("<a href=\"#references\">2. Sources of further information and references</a>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">2. Sources of further information and references</h2>", html);
        Assert.Contains("<h3 class=\"rpt-references-section\">1. Description</h3>", html);
        Assert.Contains("<h4 class=\"rpt-references-title\">Scientific paper reference</h4>", html);
        Assert.Contains("<td>1.1, 2.4</td>", html);
        Assert.Contains("<td>Smith 2020</td>", html);
        Assert.Contains("<em>- No legislative references supplied -</em>", html);
    }

    [Fact]
    public async Task RendersJustTheThreeReferenceTables_OnALandscapePage_ForASingleSectionPrint()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);
        var references = new[]
        {
            new ReportReferenceSectionView(
                "2. Two",
                [
                    new ReportReferenceTableView("Scientific paper reference", "- No scientific paper references supplied -", [new ReportReferenceRow("2.1", "Smith 2020")]),
                    new ReportReferenceTableView("Legislative reference", "- No legislative references supplied -", []),
                    new ReportReferenceTableView("Source of further information", "- No sources of further information supplied -", [])
                ])
        };

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, content.Sections[1].Id, references));

        Assert.Contains("<section id=\"references\" class=\"rpt-landscape\">", html);
        Assert.Contains("<h4 class=\"rpt-references-title\">Scientific paper reference</h4>", html);
        Assert.Contains("<h4 class=\"rpt-references-title\">Legislative reference</h4>", html);
        Assert.Contains("<h4 class=\"rpt-references-title\">Source of further information</h4>", html);
        Assert.Contains("<td>Smith 2020</td>", html);
        Assert.DoesNotContain("Sources of further information and references", html);
        Assert.DoesNotContain("<h3 class=\"rpt-references-section\">", html);
        Assert.DoesNotContain("href=\"#references\"", html);
    }

    [Fact]
    public async Task OmitsTheFrontPage_ForASingleSectionPrint()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);

        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(content, content.Sections[1].Id, []));

        Assert.DoesNotContain("<section class=\"rpt-front\">", html);
        LogoHeaderAssertions.AssertNone(html);
        Assert.Contains("2. Two", html);
        Assert.DoesNotContain("1. One", html);
    }

    [Fact]
    public async Task PrintsTheLogosAtTheTopOfTheFrontPage_AheadOfTheTitle()
    {
        var html = await RenderAsync(ProfileReportDocuments.BuildFullProfile(ReportContent.Create([ReportContent.Section(1, "One")]), Guid.Empty, []));

        LogoHeaderAssertions.AssertOnce(html);
        var front = html.IndexOf("<section class=\"rpt-front\">", StringComparison.Ordinal);
        var logos = html.IndexOf("<header class=\"logo-header", StringComparison.Ordinal);
        Assert.True(front >= 0 && front < logos);
        Assert.True(logos < html.IndexOf("<h1 class=\"rpt-title\">", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintsTheLogosOnTheFrontPageOfTheSummaryProfile_AndTheQABrief_AsTheLegacyReportsDid()
    {
        var content = ReportContent.Create([]);

        LogoHeaderAssertions.AssertOnce(await RenderAsync(ProfileReportDocuments.BuildSummaryProfile(content)));
        LogoHeaderAssertions.AssertOnce(await RenderAsync(ProfileReportDocuments.BuildQABrief(content)));
    }

    private async Task<string> RenderAsync(ProfileReportDocumentViewModel document)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        return await scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>().RenderAsync(ProfileReportDocumentPdfBuilder.ViewPath, document);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
