using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Tests.Infrastructure;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the real Create bespoke report page; only the CDC.Api clients and the PDF builder are faked.
public partial class CreateBespokeReportIntegrationTests
{
    private static readonly ProfileReportQuestionDto Question = ReportContent.Answered(1, "Is it <em>here</em>?", "a");
    private static readonly ProfileReportSectionDto Section = ReportContent.Section(1, "Description", "", Question);
    private static readonly ProfileReportContentDto Content = ReportContent.Create([Section]);
    private static readonly Guid ProfileVersionId = Content.ProfileVersionId;
    private static readonly BespokeReportTemplateDto Template = new()
    {
        Id = Guid.NewGuid(),
        Title = "Alpha",
        CheckedList = [BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section), BespokeReportNodes.Value(Question.Id, BespokeNodeType.Guidance)]
    };

    private static string PageUrl => $"/SurveillanceProfiles/CreateBespokeReport/{ProfileVersionId}";

    [Fact]
    public async Task Page_RendersTheBreadcrumbTitleIntroDropdownTreeAndButtons_ForAProfileEditor()
    {
        using var factory = CreateFactory(new FakeBespokeReportTemplatesApiService { ListResult = new(BespokeTemplatesOutcome.Success, [new BespokeReportTemplateSummaryDto { Id = Template.Id, Title = "Alpha" }]) });
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync(PageUrl);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("D2R2 Home", body);
        Assert.Contains("Search disease profiles", body);
        Assert.Contains($"/SurveillanceProfiles/ProfileReports/{ProfileVersionId}", body);
        Assert.Contains("govuk-breadcrumbs__list-item\" aria-current=\"page\"", body);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Create bespoke report</h1>", body);
        Assert.Contains(CreateBespokeReportIntro, body);
        Assert.Contains("Use previously created template:", body);
        Assert.Contains("Select template (optional)", body);
        Assert.Contains(">Alpha</option>", body);
        Assert.Contains("data-app-tree-cascade=\"true\"", body);
        Assert.Contains("Anthrax (What if)", body);
        Assert.Contains("1 : Description", body);
        Assert.Contains("1 Guidance : Is it here?", body);
        Assert.Contains("1 : Is it here?", body);
        Assert.Contains("data-app-tree-kind=\"guidance\"", body);
        Assert.Contains($"value=\"{BespokeReportNodes.Value(Question.Id, BespokeNodeType.Question)}\"", body);
        Assert.Contains("Exclude guidance questions", body);
        Assert.Contains("Include guidance questions", body);
        Assert.Contains("Remove selection", body);
        Assert.Contains("Save template as", body);
        Assert.Contains("Generate bespoke report", body);
        Assert.Contains("id=\"save-as-dialog\"", body);
        Assert.Contains("data-open-on-load=\"false\"", body);
        Assert.DoesNotContain("data-open-on-load=\"true\"", body);
        Assert.Contains("js/bespoke-report.js", body);
        Assert.DoesNotContain("Update template", body);
        Assert.DoesNotContain("Delete template", body);
        Assert.DoesNotContain("id=\"edit-title-dialog\"", body);
    }

    [Fact]
    public async Task Page_WithATemplateSelected_ShowsItsTickedNodesAndTheTemplateButtons()
    {
        using var factory = CreateFactory(new FakeBespokeReportTemplatesApiService
        {
            ListResult = new(BespokeTemplatesOutcome.Success, [new BespokeReportTemplateSummaryDto { Id = Template.Id, Title = "Alpha" }]),
            GetResult = new(BespokeTemplatesOutcome.Success, Template)
        });
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync($"{PageUrl}?templateId={Template.Id}");

        Assert.Contains($"<option value=\"{Template.Id}\" selected=\"selected\">Alpha</option>", body);
        Assert.Contains("Update template", body);
        Assert.Contains("Delete template", body);
        Assert.Contains("Edit title", body);
        Assert.Contains("Are you sure you want to delete this template?", body);
        Assert.DoesNotContain("data-open-on-load=\"true\"", body);
        Assert.Contains($"<input type=\"hidden\" name=\"TemplateId\" value=\"{Template.Id}\" />", body);
        Assert.Matches(CheckedInput(BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section)), body);
        Assert.Matches(CheckedInput(BespokeReportNodes.Value(Question.Id, BespokeNodeType.Guidance)), body);
        Assert.DoesNotMatch(CheckedInput(BespokeReportNodes.Value(Question.Id, BespokeNodeType.Question)), body);
    }

    [Fact]
    public async Task Page_ShowsTheErrorSummaryAndNoForm_WhenItCannotBeLoaded()
    {
        using var factory = CreateFactory(new FakeBespokeReportTemplatesApiService(), new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Error)));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync(PageUrl);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("We could not load the bespoke report page", body);
        Assert.DoesNotContain("bespoke-report-form", body);
    }

    [Fact]
    public async Task Page_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        using var factory = CreateFactory(new FakeBespokeReportTemplatesApiService(), new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.NotFound)));
        var client = await factory.SignedInClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(PageUrl)).StatusCode);
    }

    [Fact]
    public async Task Generate_PostsTheTickedNodes_AndDownloadsThePdf()
    {
        var builder = new FakeBespokeReportPdfBuilder();
        using var factory = CreateFactory(new FakeBespokeReportTemplatesApiService(), builder: builder);
        var client = await factory.SignedInClientAsync();
        var token = ExtractToken(await client.GetStringAsync(PageUrl));

        var response = await client.PostAsync(
            $"{PageUrl}?handler=Generate",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("CheckedNodes", BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section)),
                new("CheckedNodes", BespokeReportNodes.Value(Question.Id, BespokeNodeType.Question))
            ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Bespoke Profile Report.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal([Section.Id], builder.Selection!.Sections);
        Assert.Equal([Question.Id], builder.Selection.Questions);
    }

    [Fact]
    public async Task SaveAs_PostsTheTitleAndTickedNodes_ThenShowsTheSavedTemplateWithASuccessMessage()
    {
        var saved = new BespokeReportTemplateDto { Id = Template.Id, Title = "My template", CheckedList = Template.CheckedList };
        var templates = new FakeBespokeReportTemplatesApiService
        {
            SaveAsResult = new(BespokeTemplatesOutcome.Success, saved),
            ListResult = new(BespokeTemplatesOutcome.Success, [new BespokeReportTemplateSummaryDto { Id = saved.Id, Title = saved.Title }]),
            GetResult = new(BespokeTemplatesOutcome.Success, saved)
        };
        using var factory = CreateFactory(templates);
        var client = await factory.SignedInClientAsync();
        var token = ExtractToken(await client.GetStringAsync(PageUrl));

        var response = await client.PostAsync(
            $"{PageUrl}?handler=SaveAs",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("NewTemplateTitle", "My template"),
                new("CheckedNodes", BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section))
            ]));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Template saved", body);
        Assert.Contains($"<option value=\"{saved.Id}\" selected=\"selected\">My template</option>", body);
        Assert.Equal("My template", templates.SavedAs!.Value.Title);
        Assert.Equal([BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section)], templates.SavedAs.Value.CheckedList);
    }

    [Fact]
    public async Task SaveAs_ReopensTheDialogWithTheError_WhenTheTitleIsInUse()
    {
        var templates = new FakeBespokeReportTemplatesApiService { SaveAsResult = new(BespokeTemplatesOutcome.Conflict) };
        using var factory = CreateFactory(templates);
        var client = await factory.SignedInClientAsync();
        var token = ExtractToken(await client.GetStringAsync(PageUrl));

        var response = await client.PostAsync(
            $"{PageUrl}?handler=SaveAs",
            new FormUrlEncodedContent([new("__RequestVerificationToken", token), new("NewTemplateTitle", "Alpha"), new("CheckedNodes", BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section))]));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Template with that title already exists.", body);
        Assert.Contains("data-open-on-load=\"true\"", body);
        Assert.Contains("value=\"Alpha\"", body);
        Assert.Matches(CheckedInput(BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section)), body);
    }

    private const string CreateBespokeReportIntro =
        "Using the tree below, select sections, questions and guidance to create a bespoke report on the selected disease profile.";

    private static Regex CheckedInput(string value) => new($"<input[^>]*value=\"{Regex.Escape(value)}\"[^>]*checked", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static string ExtractToken(string html)
    {
        var match = TokenPattern().Match(html);
        Assert.True(match.Success, "The page has no antiforgery token.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    private static WebApplicationFactory<Program> CreateFactory(
        FakeBespokeReportTemplatesApiService templates,
        FakeProfileReportsApiService? reports = null,
        FakeBespokeReportPdfBuilder? builder = null) =>
        new CdcWebTestFactory().WithWebHostBuilder(webHost =>
            webHost.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton<IProfileReportsApiService>(reports ?? new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, Content)));
                services.RemoveAll<IBespokeReportTemplatesApiService>();
                services.AddSingleton<IBespokeReportTemplatesApiService>(templates);
                services.RemoveAll<IBespokeReportPdfBuilder>();
                services.AddSingleton<IBespokeReportPdfBuilder>(builder ?? new FakeBespokeReportPdfBuilder());
            }));
}
