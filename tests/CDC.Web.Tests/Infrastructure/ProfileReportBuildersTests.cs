using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileReportReferenceLoaderTests
{
    private static readonly Guid ScientificTypeId = Guid.NewGuid();
    private static readonly Guid LegislativeTypeId = Guid.NewGuid();

    [Fact]
    public async Task LoadAsync_GroupsEachKindOfReferenceUnderItsSections_InAlphabeticalOrderWithQuestionNumbers()
    {
        var first = ReportContent.Question(1, "One");
        var second = ReportContent.Question(4, "Four");
        var content = ReportContent.Create([ReportContent.Section(1, "Description", questions: first), ReportContent.Section(2, "Geography", questions: second)]);
        var sectionOne = content.Sections[0];
        var sectionTwo = content.Sections[1];
        var api = new FakeProfileSectionsApiService(
            noteTypes:
            [
                new ProfileNoteTypeDto { Id = ScientificTypeId, Name = "Scientific paper reference" },
                new ProfileNoteTypeDto { Id = LegislativeTypeId, Name = "Legislative reference" },
                new ProfileNoteTypeDto { Id = Guid.NewGuid(), Name = "Comment" }
            ],
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [ScientificTypeId] =
                [
                    Note("<p>Zebra et al</p>", (sectionOne.Id, first.Id)),
                    Note("alpha et al", (sectionOne.Id, first.Id), (sectionTwo.Id, second.Id)),
                    Note("unlinked", (Guid.NewGuid(), Guid.NewGuid()))
                ],
                [LegislativeTypeId] = [Note("Act 1999", (sectionTwo.Id, second.Id))]
            });

        var references = await new ProfileReportReferenceLoader(api).LoadAsync(content, Guid.Empty, CancellationToken.None);

        Assert.Equal(["1. Description", "2. Geography"], references.Select(section => section.Heading));
        var descriptionTables = references[0].Tables;
        Assert.Equal(["Scientific paper reference", "Legislative reference", "Source of further information"], descriptionTables.Select(table => table.Title));
        Assert.Equal(["alpha et al", "<p>Zebra et al</p>"], descriptionTables[0].Rows.Select(row => row.NoteHtml));
        Assert.Equal(["1.1, 2.4", "1.1"], descriptionTables[0].Rows.Select(row => row.QuestionReferences));
        Assert.Empty(descriptionTables[1].Rows);
        Assert.Equal("- No legislative references supplied -", descriptionTables[1].EmptyText);
        Assert.Empty(descriptionTables[2].Rows);
        Assert.Equal("- No sources of further information supplied -", descriptionTables[2].EmptyText);
        Assert.Equal("- No scientific paper references supplied -", descriptionTables[0].EmptyText);
        var geographyTables = references[1].Tables;
        Assert.Equal(["alpha et al"], geographyTables[0].Rows.Select(row => row.NoteHtml));
        Assert.Equal(["Act 1999"], geographyTables[1].Rows.Select(row => row.NoteHtml));
    }

    [Fact]
    public async Task LoadAsync_LoadsOnlyTheRequestedSection()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);

        var references = await new ProfileReportReferenceLoader(new FakeProfileSectionsApiService())
            .LoadAsync(content, content.Sections[1].Id, CancellationToken.None);

        Assert.Equal("2. Two", Assert.Single(references).Heading);
    }

    private static ProfileNoteDto Note(string text, params (Guid SectionId, Guid QuestionId)[] references) => new()
    {
        Id = Guid.NewGuid(),
        NoteText = text,
        QuestionReferences = [.. references.Select(reference => new QuestionReferenceDto { ProfileSectionId = reference.SectionId, ProfileQuestionId = reference.QuestionId })]
    };
}

public class ReportRichTextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToHtml_ReturnsNothing_ForBlankText(string? text) =>
        Assert.Equal(string.Empty, ReportRichText.ToHtml(text).ToString());

    [Fact]
    public void ToHtml_StripsTheWrappingParagraph_AndKeepsFormattingAndLinks()
    {
        var html = ReportRichText.ToHtml("<p>Hello <strong>world</strong> <a href=\"https://example.test\">link</a></p>").ToString();

        Assert.Equal("Hello <strong>world</strong> <a href=\"https://example.test\">link</a>", html);
    }

    [Fact]
    public void ToHtml_TurnsParagraphBreaksAndLineBreaksIntoLineBreaks()
    {
        Assert.Equal("One<br><br>Two", ReportRichText.ToHtml("<p>One</p><p>Two</p>").ToString());
        Assert.Equal("One<br>Two<br>Three", ReportRichText.ToHtml("One\r\nTwo\nThree").ToString());
    }

    [Fact]
    public void ToHtml_KeepsASpaceBetweenALinkAndItalicText()
    {
        var html = ReportRichText.ToHtml("<a href=\"https://example.test\">link</a><em>note</em>").ToString();

        Assert.Contains("</a>&nbsp;<em>note</em>", html);
    }

    [Fact]
    public void ToHtml_RemovesScriptsEventHandlersAndUnsafeLinks()
    {
        var html = ReportRichText.ToHtml("<p onclick=\"x()\">Safe</p><script>alert(1)</script><a href=\"javascript:alert(1)\">bad</a><img src=\"x\" onerror=\"y()\">").ToString();

        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Safe", html);
    }
}

public class ProfileReportPdfBuilderTests
{
    private static readonly byte[] Pdf = "%PDF-1.7 built"u8.ToArray();

    [Fact]
    public async Task SummaryBuilder_ReadsTheContent_RendersTheDocumentView_AndPrintsItWithTheReportHeader()
    {
        var versionId = Guid.NewGuid();
        var content = ReportContent.Create([ReportContent.Section(1, "Description", questions: ReportContent.Answered(1, "What?", "x"))]);
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, content));
        var renderer = new RecordingRenderer("<html>summary</html>");
        var pdfService = new RecordingPdfService(Pdf);
        var builder = new SummaryProfileReportPdfBuilder(api, renderer, pdfService);

        var result = await builder.BuildAsync(versionId, CancellationToken.None);

        Assert.Equal("SummaryProfileGUID", builder.ReportName);
        Assert.Equal(Pdf, result);
        Assert.Equal(versionId, api.RequestedContentVersionId);
        Assert.Equal(ProfileReportDocumentPdfBuilder.ViewPath, renderer.ViewPath);
        var document = Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model);
        Assert.Equal("Summary profile", document.ReportTitle);
        Assert.Equal("<html>summary</html>", pdfService.Html);
        Assert.Contains("Summary profile for:", pdfService.Options!.HeaderHtml);
    }

    [Fact]
    public async Task QABriefBuilder_BuildsTheBrief()
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, ReportContent.Create([])));
        var renderer = new RecordingRenderer("<html/>");
        var builder = new QABriefReportPdfBuilder(api, renderer, new RecordingPdfService(Pdf));

        await builder.BuildAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("QABriefGUID", builder.ReportName);
        Assert.Equal("Question and answer brief", Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model).ReportTitle);
    }

    [Fact]
    public async Task FullProfileBuilder_IncludesTheReferences_AndCanPrintOneSection()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, content));
        var renderer = new RecordingRenderer("<html/>");
        var builder = new FullProfileReportPdfBuilder(api, renderer, new RecordingPdfService(Pdf), new ProfileReportReferenceLoader(new FakeProfileSectionsApiService()));

        await builder.BuildAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("FullProfileGUID", builder.ReportName);
        var all = Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model);
        Assert.True(all.ShowFrontPage);
        Assert.Equal(2, all.References!.Count);

        var result = await builder.BuildSectionAsync(Guid.NewGuid(), content.Sections[1].Id, CancellationToken.None);

        Assert.Equal(Pdf, result);
        var single = Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model);
        Assert.False(single.ShowFrontPage);
        Assert.Equal("2. Two", Assert.Single(single.Sections).Heading);
        Assert.Equal("2. Two", Assert.Single(single.References!).Heading);
    }

    [Theory]
    [InlineData(ProfileReportsOutcome.NotFound)]
    [InlineData(ProfileReportsOutcome.Forbidden)]
    [InlineData(ProfileReportsOutcome.Error)]
    public async Task Builders_Throw_WhenTheContentCannotBeRead(ProfileReportsOutcome outcome)
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(outcome));
        var builder = new SummaryProfileReportPdfBuilder(api, new RecordingRenderer("<html/>"), new RecordingPdfService(Pdf));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Contains(outcome.ToString(), exception.Message);
    }

    private sealed class RecordingRenderer(string html) : IRazorViewRenderer
    {
        public string? ViewPath { get; private set; }

        public object? Model { get; private set; }

        public Task<string> RenderAsync<TModel>(string viewPath, TModel model)
        {
            ViewPath = viewPath;
            Model = model;

            return Task.FromResult(html);
        }
    }

    private sealed class RecordingPdfService(byte[] pdf) : IPlaywrightPdfService
    {
        public string? Html { get; private set; }

        public PdfRenderOptions? Options { get; private set; }

        public Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
        {
            Html = html;
            Options = options;

            return Task.FromResult(pdf);
        }
    }
}
