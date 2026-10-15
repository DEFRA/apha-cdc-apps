using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Infrastructure;

public class ProfilePrioritisationDocumentsTests
{
    private static readonly Guid Welfare = Guid.NewGuid();
    private static readonly Guid PublicHealth = Guid.NewGuid();
    private static readonly Guid PostMitigation = Guid.NewGuid();
    private static readonly Guid Cattle = Guid.NewGuid();
    private static readonly Guid Sheep = Guid.NewGuid();
    private static readonly Guid Pigs = Guid.NewGuid();
    private static readonly Guid Deer = Guid.NewGuid();
    private static readonly Guid PainCriterion = Guid.NewGuid();
    private static readonly Guid CostCriterion = Guid.NewGuid();

    [Fact]
    public void Build_PrintsOnlyAMessage_WhenPrioritisationHasNotBeenRun()
    {
        var document = ProfilePrioritisationDocuments.Build(ReportContent.Create([]), new ProfilePrioritisationScoresDto());

        Assert.False(document.HasData);
        Assert.Empty(document.Blocks);
        Assert.Empty(document.CriteriaPages);
        Assert.Equal("Summary prioritisation report", document.ReportTitle);
    }

    [Fact]
    public void Build_LaysOutTheSummaryInLegacyOrder_WithCategoryTablesOfProfiledSpeciesOnly()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(1, "Description", questions: ReportContent.Answered(1, "Technical", "a")),
            ReportContent.Section(6, "Human", questions: ReportContent.Answered(14, "Technical", "a")),
            ReportContent.Section(15, "Perception", questions: ReportContent.Answered(1, "Technical", "a"))
        ]);

        var document = ProfilePrioritisationDocuments.Build(content, Scores());

        Assert.True(document.HasData);
        Assert.Equal(
            ["Summary description", "RFI 1 Protect human health", null, null, null, null, null, null, "Public perception"],
            document.Blocks.Select(block => block.Question?.Heading));
        Assert.Equal(5, document.Blocks.Count(block => block.CategoryTable is not null));
        var firstTable = document.Blocks.First(block => block.CategoryTable is not null).CategoryTable!;
        Assert.Equal(["Cattle", "Sheep"], firstTable.Rows.Select(row => row.SpeciesName));
        Assert.Equal(["2", "- Data incomplete -"], firstTable.Rows.Select(row => row.Rank));
        Assert.Equal([false, true], firstTable.Rows.Select(row => row.IsPlaceholder));
        Assert.Contains(document.Blocks, block => block.Note == ProfilePrioritisationDocuments.StandardisedEvidenceNote);
    }

    [Fact]
    public void Build_AddsThePotentialToAddValueSection_OnlyWhenThatCategoryExists()
    {
        var content = ReportContent.Create([ReportContent.Section(14, "Opportunity", questions: ReportContent.Answered(1, "Technical", "a"))]);
        var withCategory = Scores() with
        {
            CategoryScores = [.. Scores().CategoryScores, new ProfileCategoryScoreDto { CategoryId = Guid.NewGuid(), CategoryName = "Potential to add value", SpeciesId = Cattle, SpeciesName = "Cattle", Rank = 1, IsProfiledSpecies = true }]
        };

        var with = ProfilePrioritisationDocuments.Build(content, withCategory);
        var without = ProfilePrioritisationDocuments.Build(content, Scores());

        Assert.Contains(with.Blocks, block => block.Question?.Heading == "Opportunity to enhance risk mitigation and/or understanding");
        Assert.DoesNotContain(without.Blocks, block => block.Question?.Heading == "Opportunity to enhance risk mitigation and/or understanding");
    }

    [Fact]
    public void Build_PairsProfiledSpeciesIntoCriteriaPages_WithCategoryColoursRanksAndValues()
    {
        var document = ProfilePrioritisationDocuments.Build(ReportContent.Create([], isPublished: true, showDraftWatermark: false), Scores());

        Assert.Equal("4 March 2026", document.PublishedDate);
        Assert.Equal(2, document.CriteriaPages.Count);
        var pair = document.CriteriaPages[0];
        Assert.Equal("Detailed criteria scores", pair.Heading);
        Assert.Equal("Cattle and Sheep", pair.SpeciesHeading);
        Assert.Equal("criteria-1", pair.Anchor);
        Assert.Equal(["Cattle", "Sheep"], pair.SpeciesNames);
        var welfare = pair.Categories.Single(category => category.DisplayName == "Animal welfare");
        Assert.Equal("rgb(193, 255, 193)", welfare.BackgroundColour);
        Assert.Equal("#000", welfare.ForegroundColour);
        Assert.Equal([new PrioritisationValueView("2", false), new PrioritisationValueView("- Data incomplete -", true)], welfare.Ranks);
        Assert.Equal(["Pain", "Cost"], welfare.Criteria.Select(criterion => criterion.Name));
        Assert.Equal([new PrioritisationValueView("High", false), new PrioritisationValueView("- Data incomplete -", true)], welfare.Criteria[0].Values);
        Assert.Equal([new PrioritisationValueView("- Irrelevant category -", true), new PrioritisationValueView("Low", false)], welfare.Criteria[1].Values);
        var postMitigation = pair.Categories.Single(category => category.DisplayName == "Post mitigation");
        Assert.Equal("rgb(0, 0, 0)", postMitigation.BackgroundColour);
        Assert.Equal("#fff", postMitigation.ForegroundColour);
        var publicHealth = pair.Categories.Single(category => category.DisplayName == "Public health");
        Assert.Equal("rgb(255, 153, 204)", publicHealth.BackgroundColour);
        var single = document.CriteriaPages[1];
        Assert.Equal("Pigs", single.SpeciesHeading);
        Assert.Equal(["Pigs"], single.SpeciesNames);
        Assert.Single(single.Categories[0].Ranks);
    }

    [Fact]
    public void Build_UsesTheDefaultColours_ForAnyOtherCategory()
    {
        var other = Guid.NewGuid();
        var scores = new ProfilePrioritisationScoresDto
        {
            CategoryScores = [new ProfileCategoryScoreDto { CategoryId = other, CategoryName = "Other", SpeciesId = Cattle, SpeciesName = "Cattle", IsProfiledSpecies = true }],
            CriterionScores = [new ProfileCriterionScoreDto { CategoryId = other, CategoryName = "Other", CategoryDisplayName = "Other", CriterionId = CostCriterion, CriterionName = "Cost", SpeciesId = Cattle, CriterionValue = "x" }],
            Species = [new ProfilePrioritisedSpeciesDto { SpeciesId = Cattle, Name = "Cattle" }]
        };

        var category = ProfilePrioritisationDocuments.Build(ReportContent.Create([]), scores).CriteriaPages[0].Categories[0];

        Assert.Equal("rgb(0, 163, 59)", category.BackgroundColour);
    }

    [Fact]
    public void Build_LeavesAValueBlank_WhenASpeciesHasNoScoreForIt()
    {
        var scores = Scores() with
        {
            CriterionScores = [.. Scores().CriterionScores.Where(score => !(score.SpeciesId == Sheep && score.CriterionId == PainCriterion))]
        };

        var welfare = ProfilePrioritisationDocuments.Build(ReportContent.Create([]), scores).CriteriaPages[0].Categories.Single(category => category.DisplayName == "Animal welfare");

        Assert.Equal(new PrioritisationValueView(string.Empty, false), welfare.Criteria[0].Values[1]);
    }

    [Fact]
    public void BuildPdfOptions_RepeatsTheTitleVersionAndQualityStatementInTheFooter()
    {
        var document = ProfilePrioritisationDocuments.Build(ReportContent.Create([], isPublished: true, showDraftWatermark: false), Scores());

        var options = ProfilePrioritisationDocuments.BuildPdfOptions(document);

        Assert.Contains("Summary prioritisation report for:", options.HeaderHtml);
        Assert.Contains("Summary prioritisation report for Anthrax (What if) v1.2 Published: 4 March 2026", options.FooterHtml);
        Assert.Contains(ContributionsReportLayout.QualityStatementText, options.FooterHtml);
        Assert.Contains("totalPages", options.FooterHtml);
        Assert.True(options.GenerateOutline);
    }

    [Fact]
    public void BuildPdfOptions_OmitsThePublishedDate_ForADraft()
    {
        var document = ProfilePrioritisationDocuments.Build(ReportContent.Create([]), Scores());

        Assert.DoesNotContain("Published:", ProfilePrioritisationDocuments.BuildPdfOptions(document).FooterHtml);
    }

    internal static ProfilePrioritisationScoresDto Scores() => new()
    {
        CategoryScores =
        [
            new ProfileCategoryScoreDto { CategoryId = PublicHealth, CategoryName = "Public health", SpeciesId = Cattle, SpeciesName = "Cattle", Rank = 2, IsProfiledSpecies = true },
            new ProfileCategoryScoreDto { CategoryId = PublicHealth, CategoryName = "public health", SpeciesId = Sheep, SpeciesName = "Sheep", DataIncomplete = true, IsProfiledSpecies = true },
            new ProfileCategoryScoreDto { CategoryId = PublicHealth, CategoryName = "Public health", SpeciesId = Deer, SpeciesName = "Deer", Rank = 9, IsProfiledSpecies = false },
            new ProfileCategoryScoreDto { CategoryId = Welfare, CategoryName = "Welfare", SpeciesId = Cattle, SpeciesName = "Cattle", Rank = 2, IsProfiledSpecies = true },
            new ProfileCategoryScoreDto { CategoryId = Welfare, CategoryName = "Welfare", SpeciesId = Sheep, SpeciesName = "Sheep", DataIncomplete = true, IsProfiledSpecies = true },
            new ProfileCategoryScoreDto { CategoryId = Welfare, CategoryName = "Welfare", SpeciesId = Pigs, SpeciesName = "Pigs", Rank = 5, IsProfiledSpecies = true },
            new ProfileCategoryScoreDto { CategoryId = PostMitigation, CategoryName = "Post mitigation risk", SpeciesId = Cattle, SpeciesName = "Cattle", Rank = 1, IsProfiledSpecies = true }
        ],
        CriterionScores =
        [
            Criterion(Welfare, "Welfare", "Animal welfare", PainCriterion, "Pain", Cattle, value: "High"),
            Criterion(Welfare, "Welfare", "Animal welfare", PainCriterion, "Pain", Sheep, incomplete: true),
            Criterion(Welfare, "Welfare", "Animal welfare", CostCriterion, "Cost", Cattle, irrelevant: true),
            Criterion(Welfare, "Welfare", "Animal welfare", CostCriterion, "Cost", Sheep, value: "Low"),
            Criterion(PostMitigation, "Post mitigation risk", "Post mitigation", Guid.NewGuid(), "Risk", Cattle, value: "Medium"),
            Criterion(PublicHealth, "Public health", "Public health", Guid.NewGuid(), "Zoonotic", Cattle, value: "No"),
            Criterion(Welfare, "Welfare", "Animal welfare", PainCriterion, "Pain", Pigs, value: "Mild")
        ],
        Species =
        [
            new ProfilePrioritisedSpeciesDto { SpeciesId = Cattle, Name = "Cattle" },
            new ProfilePrioritisedSpeciesDto { SpeciesId = Sheep, Name = "Sheep" },
            new ProfilePrioritisedSpeciesDto { SpeciesId = Deer, Name = "Deer" },
            new ProfilePrioritisedSpeciesDto { SpeciesId = Pigs, Name = "Pigs" },
            new ProfilePrioritisedSpeciesDto { SpeciesId = Guid.NewGuid(), Name = "Unscored" }
        ]
    };

    private static ProfileCriterionScoreDto Criterion(
        Guid categoryId,
        string categoryName,
        string displayName,
        Guid criterionId,
        string criterionName,
        Guid speciesId,
        string value = "",
        bool incomplete = false,
        bool irrelevant = false) => new()
        {
            CategoryId = categoryId,
            CategoryName = categoryName,
            CategoryDisplayName = displayName,
            CriterionId = criterionId,
            CriterionName = criterionName,
            SpeciesId = speciesId,
            CriterionValue = value,
            DataIncomplete = incomplete,
            IrrelevantCategory = irrelevant
        };
}

public class SummaryPrioritisationReportPdfBuilderTests
{
    private static readonly byte[] Pdf = "%PDF-1.7 prioritisation"u8.ToArray();

    [Fact]
    public async Task BuildAsync_ReadsContentAndScores_RendersTheView_AndPrintsWithTheFooter()
    {
        var versionId = Guid.NewGuid();
        var api = new FakeProfileReportsApiService(
            content: new ProfileReportContentResult(ProfileReportsOutcome.Success, ReportContent.Create([])),
            prioritisationScores: new ProfilePrioritisationScoresResult(ProfileReportsOutcome.Success, ProfilePrioritisationDocumentsTests.Scores()));
        var renderer = new RecordingRenderer();
        var pdfService = new RecordingPdfService();
        var builder = new SummaryPrioritisationReportPdfBuilder(api, renderer, pdfService);

        var result = await builder.BuildAsync(versionId, CancellationToken.None);

        Assert.Equal("SummaryPrioritisationGUID", builder.ReportName);
        Assert.Equal(Pdf, result);
        Assert.Equal(versionId, api.RequestedContentVersionId);
        Assert.Equal(SummaryPrioritisationReportPdfBuilder.ViewPath, renderer.ViewPath);
        Assert.IsType<PrioritisationDocumentViewModel>(renderer.Model);
        Assert.Contains("Summary prioritisation report for Anthrax (What if) v1.2", pdfService.Options!.FooterHtml);
    }

    [Theory]
    [InlineData(ProfileReportsOutcome.NotFound, ProfileReportsOutcome.Success)]
    [InlineData(ProfileReportsOutcome.Success, ProfileReportsOutcome.Error)]
    [InlineData(ProfileReportsOutcome.Forbidden, ProfileReportsOutcome.Forbidden)]
    public async Task BuildAsync_Throws_WhenEitherCallFails(ProfileReportsOutcome contentOutcome, ProfileReportsOutcome scoresOutcome)
    {
        var api = new FakeProfileReportsApiService(
            content: new ProfileReportContentResult(contentOutcome, contentOutcome == ProfileReportsOutcome.Success ? ReportContent.Create([]) : null),
            prioritisationScores: new ProfilePrioritisationScoresResult(scoresOutcome, scoresOutcome == ProfileReportsOutcome.Success ? new ProfilePrioritisationScoresDto() : null));
        var builder = new SummaryPrioritisationReportPdfBuilder(api, new RecordingRenderer(), new RecordingPdfService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Contains($"content {contentOutcome}", exception.Message);
        Assert.Contains($"scores {scoresOutcome}", exception.Message);
    }

    private sealed class RecordingRenderer : IRazorViewRenderer
    {
        public string? ViewPath { get; private set; }

        public object? Model { get; private set; }

        public Task<string> RenderAsync<TModel>(string viewPath, TModel model)
        {
            ViewPath = viewPath;
            Model = model;

            return Task.FromResult("<html/>");
        }
    }

    private sealed class RecordingPdfService : IPlaywrightPdfService
    {
        public PdfRenderOptions? Options { get; private set; }

        public Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
        {
            Options = options;

            return Task.FromResult(Pdf);
        }
    }
}

public class SummaryPrioritisationRenderingTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory factory;

    public SummaryPrioritisationRenderingTests(CdcWebTestFactory factory) => this.factory = factory;

    [Fact]
    public async Task Renders_TheSummary_CategoryTables_CriteriaPages_AndTheWarning()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(1, "Description", questions: ReportContent.Answered(1, "Technical", "An answer")),
            ReportContent.Section(6, "Human", questions: ReportContent.Answered(14, "Technical", "Public"))
        ]);

        var html = await RenderAsync(ProfilePrioritisationDocuments.Build(content, ProfilePrioritisationDocumentsTests.Scores()));

        Assert.Contains("Summary prioritisation report for: Anthrax <em>(What if)</em>", html);
        Assert.Contains("class=\"rpt-watermark\"", html);
        Assert.Contains("<h3>Summary description</h3>", html);
        Assert.Contains("<h3>RFI 1 Protect human health</h3>", html);
        Assert.Contains("<table class=\"rpt-category\">", html);
        Assert.Contains("<span class=\"rpt-placeholder\">- Data incomplete -</span>", html);
        Assert.Contains("<p class=\"rpt-note-italic\">The following information is derived from evidence", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Detailed criteria scores: Cattle and Sheep</h2>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Detailed criteria scores: Pigs</h2>", html);
        Assert.Contains("style=\"background-color: rgb(193, 255, 193); color: #000;\"", html);
        Assert.Contains("<span class=\"rpt-placeholder\">- Irrelevant category -</span>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Please Note</h2>", html);
    }

    [Fact]
    public async Task Renders_OnlyTheNoDataMessage_WhenPrioritisationHasNotBeenRun()
    {
        var html = await RenderAsync(ProfilePrioritisationDocuments.Build(ReportContent.Create([], isPublished: true, showDraftWatermark: false), new ProfilePrioritisationScoresDto()));

        Assert.Contains("Prioritisation data has not been generated for this profile version.", html);
        Assert.DoesNotContain("class=\"rpt-watermark\"", html);
        Assert.DoesNotContain("<table class=\"rpt-category\">", html);
        Assert.DoesNotContain("Detailed criteria scores", html);
    }

    private async Task<string> RenderAsync(PrioritisationDocumentViewModel document)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        return await scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>().RenderAsync(SummaryPrioritisationReportPdfBuilder.ViewPath, document);
    }
}
