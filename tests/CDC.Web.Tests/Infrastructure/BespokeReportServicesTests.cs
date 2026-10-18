using System.Net;
using System.Net.Http.Json;
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

public class BespokeReportTemplatesApiServiceTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly BespokeReportTemplateDto Template = new() { Id = TemplateId, Title = "Alpha", CheckedList = ["a", "b"] };

    [Fact]
    public async Task GetTemplatesAsync_ReturnsTheTemplates()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(new[] { new BespokeReportTemplateSummaryDto { Id = TemplateId, Title = "Alpha" } }));

        var result = await CreateService(handler).GetTemplatesAsync();

        Assert.Equal(BespokeTemplatesOutcome.Success, result.Outcome);
        Assert.Equal("Alpha", Assert.Single(result.Templates!).Title);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/bespoke-report-templates", handler.RequestPath);
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var result = await CreateService(new RecordingHandler(HttpStatusCode.OK, Json("null"))).GetTemplatesAsync();

        Assert.Equal(BespokeTemplatesOutcome.Error, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, BespokeTemplatesOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, BespokeTemplatesOutcome.Error)]
    public async Task GetTemplatesAsync_MapsFailureStatusCodes(HttpStatusCode status, BespokeTemplatesOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetTemplatesAsync();

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Templates);
    }

    [Fact]
    public async Task GetTemplateAsync_ReturnsTheTemplateWithItsTickedItems()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(Template));

        var result = await CreateService(handler).GetTemplateAsync(TemplateId);

        Assert.Equal(BespokeTemplatesOutcome.Success, result.Outcome);
        Assert.Equal(["a", "b"], result.Template!.CheckedList);
        Assert.Equal($"/api/bespoke-report-templates/{TemplateId}", handler.RequestPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, BespokeTemplatesOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, BespokeTemplatesOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, BespokeTemplatesOutcome.Conflict)]
    [InlineData(HttpStatusCode.BadRequest, BespokeTemplatesOutcome.Invalid)]
    [InlineData(HttpStatusCode.BadGateway, BespokeTemplatesOutcome.Error)]
    public async Task GetTemplateAsync_MapsFailureStatusCodes(HttpStatusCode status, BespokeTemplatesOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetTemplateAsync(TemplateId);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Template);
    }

    [Fact]
    public async Task GetTemplateAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var result = await CreateService(new RecordingHandler(HttpStatusCode.OK, Json("null"))).GetTemplateAsync(TemplateId);

        Assert.Equal(BespokeTemplatesOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task SaveAsAsync_PostsTheTitleAndTickedItems()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, JsonContent.Create(Template));

        var result = await CreateService(handler).SaveAsAsync("Alpha", ["a", "b"]);

        Assert.Equal(BespokeTemplatesOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/bespoke-report-templates", handler.RequestPath);
        Assert.Contains("\"title\":\"Alpha\"", handler.RequestBody);
        Assert.Contains("\"checkedList\":[\"a\",\"b\"]", handler.RequestBody);
    }

    [Fact]
    public async Task SaveAsAsync_ReportsADuplicateTitle()
    {
        var result = await CreateService(new RecordingHandler(HttpStatusCode.Conflict, new StringContent(string.Empty))).SaveAsAsync("Alpha", []);

        Assert.Equal(BespokeTemplatesOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_PutsTheTickedItems()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(Template));

        var result = await CreateService(handler).UpdateAsync(TemplateId, ["x"]);

        Assert.Equal(BespokeTemplatesOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal($"/api/bespoke-report-templates/{TemplateId}", handler.RequestPath);
        Assert.Contains("\"checkedList\":[\"x\"]", handler.RequestBody);
    }

    [Fact]
    public async Task RenameAsync_PutsTheTitle()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(Template));

        var result = await CreateService(handler).RenameAsync(TemplateId, "Gamma");

        Assert.Equal(BespokeTemplatesOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal($"/api/bespoke-report-templates/{TemplateId}/title", handler.RequestPath);
        Assert.Contains("\"title\":\"Gamma\"", handler.RequestBody);
    }

    [Fact]
    public async Task DeleteAsync_DeletesTheTemplate()
    {
        var handler = new RecordingHandler(HttpStatusCode.NoContent, new StringContent(string.Empty));

        var outcome = await CreateService(handler).DeleteAsync(TemplateId);

        Assert.Equal(BespokeTemplatesOutcome.Success, outcome);
        Assert.Equal(HttpMethod.Delete, handler.Method);
        Assert.Equal($"/api/bespoke-report-templates/{TemplateId}", handler.RequestPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, BespokeTemplatesOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, BespokeTemplatesOutcome.Forbidden)]
    public async Task DeleteAsync_MapsFailureStatusCodes(HttpStatusCode status, BespokeTemplatesOutcome expected)
    {
        var outcome = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).DeleteAsync(TemplateId);

        Assert.Equal(expected, outcome);
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    private static BespokeReportTemplatesApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? RequestPath { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestPath = request.RequestUri!.AbsolutePath;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode) { Content = content };
        }
    }
}

public class ProfileQuestionGuidanceApiTests
{
    [Fact]
    public async Task GetQuestionGuidanceAsync_PostsTheQuestionIds_AndReturnsTheGuidance()
    {
        var questionId = Guid.NewGuid();
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonContent.Create(new[] { new ProfileQuestionGuidanceDto { QuestionId = questionId, UserGuidance = "<p>g</p>" } }));

        var result = await CreateService(handler).GetQuestionGuidanceAsync([questionId]);

        Assert.Equal(ProfileReportsOutcome.Success, result.Outcome);
        Assert.Equal("<p>g</p>", Assert.Single(result.Guidance!).UserGuidance);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/profile-reports/guidance", handler.RequestPath);
        Assert.Contains(questionId.ToString(), handler.RequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ProfileReportsOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ProfileReportsOutcome.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, ProfileReportsOutcome.Error)]
    public async Task GetQuestionGuidanceAsync_MapsFailureStatusCodes(HttpStatusCode status, ProfileReportsOutcome expected)
    {
        var result = await CreateService(new RecordingHandler(status, new StringContent(string.Empty))).GetQuestionGuidanceAsync([Guid.NewGuid()]);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Guidance);
    }

    [Fact]
    public async Task GetQuestionGuidanceAsync_ReturnsError_WhenTheBodyIsJsonNull()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(ProfileReportsOutcome.Error, (await CreateService(handler).GetQuestionGuidanceAsync([Guid.NewGuid()])).Outcome);
    }

    private static ProfileReportsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

    private sealed class RecordingHandler(HttpStatusCode statusCode, HttpContent content) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? RequestPath { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestPath = request.RequestUri!.AbsolutePath;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode) { Content = content };
        }
    }
}

public class BespokeReportPdfBuilderTests
{
    private static readonly byte[] Pdf = "%PDF-1.7 bespoke"u8.ToArray();
    private static readonly ProfileReportQuestionDto QuestionOne = ReportContent.Answered(1, "Is it here?", "a");
    private static readonly ProfileReportSectionDto Description = ReportContent.Section(1, "Description", "", QuestionOne);
    private static readonly ProfileReportContentDto Content = ReportContent.Create([Description]);

    private static BespokeReportSelection Selection(bool guidance) =>
        new(
            new HashSet<Guid> { Description.Id },
            new HashSet<Guid> { QuestionOne.Id },
            guidance ? new HashSet<Guid> { QuestionOne.Id, Guid.NewGuid() } : new HashSet<Guid>());

    [Fact]
    public async Task BuildAsync_ReadsTheContentAndGuidance_RendersTheSharedView_AndPrintsWithTheTemplateTitleInTheHeader()
    {
        var versionId = Guid.NewGuid();
        var api = new FakeProfileReportsApiService(
            content: new ProfileReportContentResult(ProfileReportsOutcome.Success, Content),
            guidance: new ProfileQuestionGuidanceResult(ProfileReportsOutcome.Success, [new ProfileQuestionGuidanceDto { QuestionId = QuestionOne.Id, UserGuidance = "Use the manual" }]));
        var renderer = new RecordingRenderer();
        var pdfService = new RecordingPdfService();

        var result = await new BespokeReportPdfBuilder(api, renderer, pdfService).BuildAsync(versionId, Selection(guidance: true), "My template", CancellationToken.None);

        Assert.Equal(Pdf, result);
        Assert.Equal(versionId, api.RequestedContentVersionId);
        Assert.Equal([QuestionOne.Id], api.RequestedGuidanceIds);
        Assert.Equal(ProfileReportDocumentPdfBuilder.ViewPath, renderer.ViewPath);
        var document = Assert.IsType<ProfileReportDocumentViewModel>(renderer.Model);
        Assert.Equal("My template", document.ReportTitle);
        var guidanceView = Assert.Single(Assert.Single(document.Sections).Questions, question => question.GuidanceHtml is not null);
        Assert.Equal("Use the manual", guidanceView.GuidanceHtml);
        Assert.Contains("My template for:", pdfService.Options!.HeaderHtml);
    }

    [Fact]
    public async Task BuildAsync_DoesNotAskForGuidance_WhenNoneIsTicked()
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, Content));

        await new BespokeReportPdfBuilder(api, new RecordingRenderer(), new RecordingPdfService()).BuildAsync(Guid.NewGuid(), Selection(guidance: false), null, CancellationToken.None);

        Assert.Null(api.RequestedGuidanceIds);
    }

    [Theory]
    [InlineData(ProfileReportsOutcome.NotFound)]
    [InlineData(ProfileReportsOutcome.Forbidden)]
    [InlineData(ProfileReportsOutcome.Error)]
    public async Task BuildAsync_Throws_WhenTheContentCannotBeRead(ProfileReportsOutcome outcome)
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(outcome));
        var builder = new BespokeReportPdfBuilder(api, new RecordingRenderer(), new RecordingPdfService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(Guid.NewGuid(), Selection(guidance: false), null, CancellationToken.None));

        Assert.Contains(outcome.ToString(), exception.Message);
    }

    [Theory]
    [InlineData(ProfileReportsOutcome.Forbidden)]
    [InlineData(ProfileReportsOutcome.Error)]
    public async Task BuildAsync_Throws_WhenTheGuidanceCannotBeRead(ProfileReportsOutcome outcome)
    {
        var api = new FakeProfileReportsApiService(
            content: new ProfileReportContentResult(ProfileReportsOutcome.Success, Content),
            guidance: new ProfileQuestionGuidanceResult(outcome));
        var builder = new BespokeReportPdfBuilder(api, new RecordingRenderer(), new RecordingPdfService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(Guid.NewGuid(), Selection(guidance: true), null, CancellationToken.None));

        Assert.Contains("guidance", exception.Message);
    }

    [Fact]
    public async Task BuildAsync_RejectsANullSelection()
    {
        var builder = new BespokeReportPdfBuilder(new FakeProfileReportsApiService(), new RecordingRenderer(), new RecordingPdfService());

        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(Guid.NewGuid(), null!, null, CancellationToken.None));
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

public class BespokeReportRenderingTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory factory;

    public BespokeReportRenderingTests(CdcWebTestFactory factory) => this.factory = factory;

    [Fact]
    public async Task Renders_TheGuidanceTable_BesideTheQuestionTable_AndSanitisesTheGuidance()
    {
        var question = ReportContent.Answered(1, "Is it here?", "An answer");
        var section = ReportContent.Section(1, "Description", "A note", question);
        var content = ReportContent.Create([section], isPublished: true, showDraftWatermark: false);
        var selection = new BespokeReportSelection(
            new HashSet<Guid> { section.Id },
            new HashSet<Guid> { question.Id },
            new HashSet<Guid> { question.Id });
        var guidance = new Dictionary<Guid, string> { [question.Id] = "<p>Read the <em>manual</em><script>alert(1)</script></p>" };
        var document = ProfileBespokeReportDocuments.Build(content, selection, guidance, "My template");

        var html = await RenderAsync(document);

        Assert.Contains("My template for: Anthrax <em>(What if)</em>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">1. Description</h2>", html);
        Assert.Contains("<h3>1.1 Is it here?</h3>", html);
        Assert.Contains("<table class=\"rpt-guidance\">", html);
        Assert.Contains("<h3>1.1 : Is it here? (Guidance)</h3>", html);
        Assert.Contains("Read the <em>manual</em>", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.True(html.IndexOf("<h3>1.1 Is it here?</h3>", StringComparison.Ordinal) < html.IndexOf("<h3>1.1 : Is it here? (Guidance)</h3>", StringComparison.Ordinal));
        Assert.Contains("<h2 class=\"rpt-section-heading\">Please Note</h2>", html);
    }

    [Fact]
    public async Task Renders_NoGuidanceTable_ForAnOrdinaryQuestion()
    {
        var question = ReportContent.Answered(1, "Is it here?", "An answer");
        var section = ReportContent.Section(1, "Description", "", question);
        var selection = new BespokeReportSelection(new HashSet<Guid> { section.Id }, new HashSet<Guid> { question.Id }, new HashSet<Guid>());

        var html = await RenderAsync(ProfileBespokeReportDocuments.Build(ReportContent.Create([section]), selection, new Dictionary<Guid, string>(), null));

        Assert.Contains("Bespoke Profile Report for: Anthrax", html);
        Assert.DoesNotContain("<table class=\"rpt-guidance\">", html);
    }

    private async Task<string> RenderAsync(ProfileReportDocumentViewModel document)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        return await scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>().RenderAsync(ProfileReportDocumentPdfBuilder.ViewPath, document);
    }
}
