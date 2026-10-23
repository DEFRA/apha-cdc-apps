using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline - routing, authorization, model binding, antiforgery, the Razor page and the real report view -
// for the Review timings (disease reviews) page. Only the CDC.Api clients and the Chromium PDF step are faked; the PDF step
// captures the HTML and footer it was asked to print.
public partial class DiseaseReviewsIntegrationTests
{
    private const string PageUrl = "/SurveillanceProfiles/DiseaseReviews";

    private static readonly byte[] PdfBytes = "%PDF-1.7 test"u8.ToArray();
    private static readonly string[] SummaryLinksInPageOrder =
        ["<a href=\"#FromDate\">", "<a href=\"#ToDate\">", "<a href=\"#AuthorId\">", "<a href=\"#DescriptionEditor\">"];
    private static readonly GlobalUserDto Jane = new() { Id = Guid.NewGuid(), FullName = "Jane Doe" };
    private static readonly GlobalUserDto Sam = new() { Id = Guid.NewGuid(), FullName = "Sam <Poe>" };

    [Fact]
    public async Task Get_ShowsTheFormWithEveryAuthor_TheFirstSelected_AndNoErrors()
    {
        using var factory = CreateFactory(ReportOf(), new CapturingPdfService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync(PageUrl);

        Assert.Contains("<title>Review timings - D2R2</title>", page);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Review timings</h1>", page);
        Assert.Contains("for=\"FromDate\">From</label>", page);
        Assert.Contains("for=\"ToDate\">To</label>", page);
        Assert.Contains("for=\"AuthorId\">Author</label>", page);
        Assert.Contains($"<option value=\"{Jane.Id}\" selected=\"selected\">Jane Doe</option>", page);
        Assert.Contains($"<option value=\"{Sam.Id}\">Sam &lt;Poe&gt;</option>", page);
        Assert.Contains("data-app-title-toolbar=\"DescriptionEditor\"", page);
        Assert.Contains("contenteditable=\"true\"", page);
        Assert.Contains("Generate report</button>", page);
        Assert.Contains("/js/edit-profile-title.js", page);
        Assert.DoesNotContain("govuk-error-summary", page);
        Assert.DoesNotContain("govuk-form-group--error", page);
    }

    [Fact]
    public async Task Get_ShowsAnErrorSummary_WhenTheAuthorsCannotBeLoaded()
    {
        using var factory = CreateFactory(ReportOf(), new CapturingPdfService(), new StubGlobalUsersApiService([], new HttpRequestException("boom")));
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync(PageUrl);

        Assert.Contains("<title>Error: Review timings - D2R2</title>", page);
        Assert.Contains("There is a problem", page);
        Assert.Contains("We could not load the list of authors. Try again later.", page);
    }

    [Fact]
    public async Task Post_ReturnsThePdf_FromTheRealReportViewWithEverythingEncoded()
    {
        var versionId = Guid.NewGuid();
        var otherVersionId = Guid.NewGuid();
        var report = ReportOf(
            new DiseaseReviewsReportRowDto
            {
                ProfileVersionId = versionId, Title = "Avian <em>influenza</em>", VersionMajor = 2, VersionMinor = 1,
                SectionNumber = 2, SectionName = "Control", NextAuthorReview = new DateTime(2027, 3, 9)
            },
            new DiseaseReviewsReportRowDto
            {
                ProfileVersionId = versionId, Title = "Avian <em>influenza</em>", VersionMajor = 2, VersionMinor = 1,
                SectionNumber = 1, SectionName = "Summary & overview",
                NextPolicyReview = new DateTime(2027, 2, 3), NextVaReview = new DateTime(2027, 6, 7)
            },
            new DiseaseReviewsReportRowDto
            {
                ProfileVersionId = otherVersionId, Title = "<script>alert(1)</script>Anthrax", VersionMajor = 1, VersionMinor = 0,
                SectionNumber = 1, SectionName = "Summary"
            });
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(report, pdf);
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "1 February 2027", "28 February 2027", Jane.Id.ToString(), "<i>Quarterly</i> <b>check</b>");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
        Assert.Matches(@"^DiseaseReviewsReport_\d{2}_\d{2}_\d{4}_\d{2}_\d{2}_\d{2}\.pdf$", fileName);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());

        var html = pdf.Html!;
        Assert.Contains("<p class=\"drr-line\"><i>Quarterly</i> <b>check</b></p>", html);
        Assert.Contains("<p class=\"drr-line\">Jane Doe due between 01-02-2027 and 28-02-2027</p>", html);
        Assert.Contains("<h1 class=\"drr-profile-name\">Avian <em>influenza</em> v2.1</h1>", html);
        Assert.Contains("<th scope=\"col\" class=\"drr-table__section\">Section</th>", html);
        Assert.Contains("<th scope=\"col\">Policy Review Due Date</th>", html);
        Assert.Contains("<th scope=\"col\">Author Review Due Date</th>", html);
        Assert.Contains("<th scope=\"col\">VA Review Due Date</th>", html);
        Assert.Matches(@"<td>1: Summary &amp; overview</td>\s*<td>03 Feb 2027</td>\s*<td></td>\s*<td>07 Jun 2027</td>", html);
        Assert.Matches(@"<td>2: Control</td>\s*<td></td>\s*<td>09 Mar 2027</td>\s*<td></td>", html);
        Assert.True(html.IndexOf("1: Summary &amp; overview", StringComparison.Ordinal) < html.IndexOf("2: Control", StringComparison.Ordinal));
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;Anthrax v1.0", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("Jane Doe due 01-02-27-28-02-27", pdf.FooterHtml);
        Assert.Contains("Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span>", pdf.FooterHtml);
    }

    [Fact]
    public async Task Post_WithNothingDue_StillReturnsThePdfWithItsHeadingAndRule()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(ReportOf(), pdf);
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "1 February 2027", "28 February 2027", Jane.Id.ToString(), string.Empty);

        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Jane Doe due between 01-02-2027 and 28-02-2027", pdf.Html);
        Assert.DoesNotContain("<h1 class=\"drr-profile-name\">", pdf.Html);
    }

    [Fact]
    public async Task Post_WithBadDates_ShowsTheErrorSummaryLinkedToTheFields_AndKeepsWhatWasTyped()
    {
        var pdf = new CapturingPdfService();
        using var factory = CreateFactory(ReportOf(), pdf);
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, string.Empty, "soon", Sam.Id.ToString(), "<i>Quarterly</i>");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(pdf.Html);
        Assert.Contains("<title>Error: Review timings - D2R2</title>", body);
        Assert.Contains("There is a problem", body);
        Assert.Contains("<a href=\"#FromDate\">Enter a from date</a>", body);
        Assert.Contains("<a href=\"#ToDate\">The to date must be a real date, for example 1 January 2027</a>", body);
        Assert.Matches("<p id=\"FromDate-error\"[^>]*>\\s*<span class=\"govuk-visually-hidden\">Error:</span> Enter a from date\\s*</p>", body);
        Assert.Contains("aria-describedby=\"FromDate-hint FromDate-error\"", body);
        Assert.Contains("aria-describedby=\"ToDate-hint ToDate-error\"", body);
        Assert.Contains("govuk-input--width-10 govuk-input--error", body);
        Assert.Contains("value=\"soon\"", body);
        Assert.Contains($"<option value=\"{Sam.Id}\" selected=\"selected\">Sam &lt;Poe&gt;</option>", body);
        Assert.Equal("<i>Quarterly</i>", HiddenValue(body, "Description"));
        Assert.Contains("><i>Quarterly</i></div>", body);
    }

    [Fact]
    public async Task Post_WithAnUnknownAuthorAndALongDescription_ShowsBothErrors()
    {
        using var factory = CreateFactory(ReportOf(), new CapturingPdfService());
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "1 February 2027", "28 February 2027", Guid.NewGuid().ToString(), new string('a', 256));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<a href=\"#AuthorId\">Select an author</a>", body);
        Assert.Contains("<a href=\"#DescriptionEditor\">Description must be 255 characters or fewer</a>", body);
    }

    [Fact]
    public async Task Post_WithEveryFieldWrong_ListsTheErrorsInTheOrderTheFieldsAppear()
    {
        using var factory = CreateFactory(ReportOf(), new CapturingPdfService());
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, string.Empty, "soon", Guid.NewGuid().ToString(), new string('a', 256));
        var body = await response.Content.ReadAsStringAsync();

        var positions = SummaryLinksInPageOrder.Select(link => body.IndexOf(link, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
    }

    [Fact]
    public async Task Post_WhenTheApiRefusesTheUser_Returns403()
    {
        var reports = new FakeProfileReportsApiService(diseaseReviews: new DiseaseReviewsReportResult(DiseaseReviewsReportOutcome.Forbidden));
        using var factory = CreateFactory(reports, new CapturingPdfService());
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "1 February 2027", "28 February 2027", Jane.Id.ToString(), string.Empty);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenThePdfCannotBeRendered_ShowsTheErrorAndKeepsTheForm()
    {
        using var factory = CreateFactory(ReportOf(), new CapturingPdfService(new PdfRenderException("no browser")));
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "1 February 2027", "28 February 2027", Jane.Id.ToString(), string.Empty);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("We could not create the disease reviews report. Try again later.", body);
        Assert.Contains("value=\"1 February 2027\"", body);
        Assert.Contains($"<option value=\"{Jane.Id}\" selected=\"selected\">Jane Doe</option>", body);
    }

    private static FakeProfileReportsApiService ReportOf(params DiseaseReviewsReportRowDto[] rows) =>
        new(diseaseReviews: new DiseaseReviewsReportResult(DiseaseReviewsReportOutcome.Success, new DiseaseReviewsReportDto { Sections = rows }));

    private static string HiddenValue(string body, string name)
    {
        var match = HiddenInputRegex(name).Match(body);
        Assert.True(match.Success, $"No hidden input named {name} was found.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    // Loads the form first, as a browser would, to pick up the antiforgery token the post needs.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string fromDate, string toDate, string authorId, string description)
    {
        var page = await client.GetStringAsync(PageUrl);
        var token = AntiforgeryTokenRegex().Match(page);
        Assert.True(token.Success, "No antiforgery token found on the Review timings page.");

        return await client.PostAsync(PageUrl, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token.Groups[1].Value),
            new KeyValuePair<string, string>("FromDate", fromDate),
            new KeyValuePair<string, string>("ToDate", toDate),
            new KeyValuePair<string, string>("AuthorId", authorId),
            new KeyValuePair<string, string>("Description", description)
        ]));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IProfileReportsApiService reports,
        IPlaywrightPdfService pdf,
        IGlobalUsersApiService? users = null) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton(reports);
                services.RemoveAll<IPlaywrightPdfService>();
                services.AddSingleton(pdf);
                services.RemoveAll<IGlobalUsersApiService>();
                services.AddSingleton(users ?? new StubGlobalUsersApiService([Jane, Sam]));
            }));

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    private static Regex HiddenInputRegex(string name) =>
        new($"""<input type="hidden"[^>]*\bname="{name}"[^>]*\bvalue="([^"]*)"[^>]*>""", RegexOptions.None, TimeSpan.FromSeconds(5));

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
