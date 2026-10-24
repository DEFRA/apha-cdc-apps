using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline - routing, authorization, query binding, the Razor page and the real report views - for the four
// guidance reports. Only the CDC.Api client and the Chromium PDF step are faked; the PDF step captures the HTML and footer it was asked to print.
public class ViewProfileGuidanceReportIntegrationTests
{
    private const string PageUrl = "/SurveillanceProfiles/ViewProfileGuidanceReport";

    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();

    [Fact]
    public async Task Get_FullReport_ReturnsAPdfAttachment_FromTheRealViewWithEverythingEncoded()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(pdf);
        var client = await EditorAsync(factory);

        var response = await client.GetAsync($"{PageUrl}?type=All");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("Profile Guidance_", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName!.Trim('"'), StringComparison.Ordinal);
        var html = pdf.Html!;
        AssertHasLogoHeaderOnce(html, "Full profile guidance report");
        Assert.Contains("<h2 class=\"gr-heading\">1 - Section 1</h2>", html);
        Assert.Contains("<th scope=\"col\" class=\"gr-no\">No.</th>", html);
        Assert.Contains("<th scope=\"col\">User Guidance</th>", html);
        Assert.Contains("<td>Q1.3 <i>name</i></td>", html);
        Assert.Contains("Guidance 1.3", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("Full profile guidance report.</td>", pdf.FooterHtml!);
    }

    [Fact]
    public async Task Get_SummaryProfile_PrintsTheTenHeadingsAndTheEndemicNote()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(pdf);
        var client = await EditorAsync(factory);

        var response = await client.GetAsync($"{PageUrl}?type=SummaryProfile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = pdf.Html!;
        AssertHasLogoHeaderOnce(html, "Summary profile guidance report");
        Assert.Contains("<h2 class=\"gr-heading\">1. Description</h2>", html);
        Assert.Contains("<h2 class=\"gr-heading\">10. Compensation</h2>", html);
        Assert.Contains("<td>5.1 (1)</td>", html);
        Assert.Contains("The first question in this section is dependent on if a disease is endemic or exotic to Great Britain.", html);
        Assert.Contains("class=\"gr-note\"", html);
    }

    [Fact]
    public async Task Get_QaSheet_PrintsTheNonTechnicalTitles_AndTheNotesInRed()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(pdf);
        var client = await EditorAsync(factory);

        var response = await client.GetAsync($"{PageUrl}?type=QAGuidanceReport");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = pdf.Html!;
        AssertHasLogoHeaderOnce(html, "Q&amp;A sheet guidance report");
        Assert.DoesNotContain("<h2 class=\"gr-heading\">", html);
        Assert.Contains("<td>Plain 6.14</td>", html);
        Assert.Contains("<td>16 1a</td>", html);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(html, "class=\"gr-note gr-note--red\""));
    }

    [Fact]
    public async Task Get_SprGuidanceReport_PrintsTheStandardText_TheTenQuestions_AndTheColouredCriteria()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(pdf);
        var client = await EditorAsync(factory);

        var response = await client.GetAsync($"{PageUrl}?type=SummaryPrioritisationReport");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = pdf.Html!;
        AssertHasLogoHeaderOnce(html, "Guidance for questions in the summary prioritisation report (SPR)");
        Assert.Contains("<h2 class=\"sp-std-title\">D2R2 Scoring: Overall Approach</h2>", html);
        Assert.Contains("<li>&#x27;to protect public health&#x27; (colour-coded pink on D2R2 outputs)</li>", html);
        Assert.Contains("<th scope=\"col\">Weighted Highest Possible Score</th>", html);
        Assert.Contains("Overall maximum score for this reason for Government intervention (RFI) =", html);
        Assert.Contains("<td class=\"sp-example__total-value\">875</td>", html);
        Assert.Contains("<h2 class=\"gr-heading\">RFI 3 Protect interests of wider economy, environment &amp; society</h2>", html);
        Assert.Contains("<h2 class=\"sp-criteria-title\">Detailed criteria scores</h2>", html);
        Assert.Contains("<h3 class=\"sp-category\" style=\"background-color:rgb(255,153,204);color:rgb(0,0,0);\">Public health</h3>", html);
        Assert.Contains("<h3 class=\"sp-category\" style=\"background-color:rgb(0,0,0);color:rgb(255,255,255);\">Post mitigation risk</h3>", html);
        Assert.Contains(">Endemic or exotic</p>", html);
        Assert.Contains("Guidance 2.4", html);
        Assert.Contains("Guidance for questions in the summary prioritisation report (SPR).</td>", pdf.FooterHtml!);
    }

    [Fact]
    public async Task Get_WithNoType_ProducesTheFullReport()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(pdf);
        var client = await EditorAsync(factory);

        var response = await client.GetAsync(PageUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Full profile guidance report", pdf.Html!);
    }

    [Fact]
    public async Task Get_WithAnUnknownType_IsABadRequest()
    {
        using var factory = CreateFactory(new CapturingPdfService());
        var client = await EditorAsync(factory);

        var response = await client.GetAsync($"{PageUrl}?type=Nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenThePdfCannotBeMade_ShowsAnErrorPageWithALinkBack()
    {
        using var factory = CreateFactory(new CapturingPdfService(new PdfRenderException("no browser")));
        var client = await EditorAsync(factory);

        var page = await client.GetStringAsync($"{PageUrl}?type=All");

        Assert.Contains("<title>Error: Guidance report - D2R2</title>", page);
        Assert.Contains("There is a problem", page);
        Assert.Contains("We could not create the guidance report. Try again later.", page);
        Assert.Contains("href=\"/SurveillanceProfiles/EditUserGuidance\">Back to edit questions and guidance</a>", page);
    }

    [Fact]
    public async Task Get_AsAUserWhoCannotEditGuidance_IsRefused()
    {
        var questions = new FakeProfileQuestionsApiService();
        using var factory = CreateFactory(new CapturingPdfService(), questions);
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: true);

        var response = await client.GetAsync($"{PageUrl}?type=All");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, questions.GuidanceContentCallCount);
    }

    // The legacy header is in the body, ahead of everything else, so it prints on the first page only.
    private static void AssertHasLogoHeaderOnce(string html, string encodedTitle)
    {
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(html, "<header class=\"gr-header\">"));
        Assert.Contains($"<h1 class=\"gr-header__title\">{encodedTitle}</h1>", html);
        Assert.Contains("alt=\"Animal and Plant Health Agency\"", html);
        Assert.Contains("alt=\"D2R2\"", html);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(html, "src=\"data:image/png;base64,iVBORw0KGgo"));
        Assert.True(html.IndexOf("<header class=\"gr-header\">", StringComparison.Ordinal) < html.IndexOf("<table", StringComparison.Ordinal));
        Assert.DoesNotContain("gr-title", html);
    }

    private static Task<HttpClient> EditorAsync(WebApplicationFactory<Program> factory) =>
        factory.SignedInClientWithPermissionsAsync(isProfileEditor: true, isPolicyProfileUser: false);

    private static WebApplicationFactory<Program> CreateFactory(CapturingPdfService pdf, FakeProfileQuestionsApiService? questions = null) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileQuestionsApiService>();
                services.AddSingleton<IProfileQuestionsApiService>(questions ?? new FakeProfileQuestionsApiService(
                    guidanceContent: new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Success, GuidanceTestContent.Build())));
                services.RemoveAll<IPlaywrightPdfService>();
                services.AddSingleton<IPlaywrightPdfService>(pdf);
            }));

    private sealed class CapturingPdfService(Exception? throwOnRender = null) : IPlaywrightPdfService
    {
        public string? Html { get; private set; }

        public string? FooterHtml { get; private set; }

        public Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
        {
            Html = html;
            FooterHtml = options.FooterHtml;

            return throwOnRender is not null ? throw throwOnRender : Task.FromResult(PdfBytes);
        }
    }
}
