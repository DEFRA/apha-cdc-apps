using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Features.DiseaseRanking;
using CDC.Web.Infrastructure;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline - model binding, the page, the Razor report view - with only CDC.Api and
// the headless-browser PDF step replaced, so no live API or Chromium install is needed.
public class DiseaseRankingIntegrationTests
{
    private const string PagePath = "/Reports/DiseaseRanking";

    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D];

    private static readonly IReadOnlyList<DiseaseRankingReportOptionDto> Options =
    [
        new DiseaseRankingReportOptionDto { Key = "terrestrial", Label = "Disease ranking report: Terrestrial diseases" },
        new DiseaseRankingReportOptionDto { Key = "aquatic", Label = "Disease ranking report: Aquatic diseases" },
        new DiseaseRankingReportOptionDto { Key = "11111111-1111-1111-1111-111111111111", Label = "All viral diseases" }
    ];

    private static DiseaseRankingReportDto SampleReport(DiseaseRankingReportKind kind) => new()
    {
        Kind = kind,
        Title = "Issues ranked for Terrestrial Diseases",
        Subtitle = "Subtitle text",
        FooterTitle = "Footer title.",
        FileName = $"DiseaseRankingReport_{kind}_Terrestrial",
        TopTables =
        [
            new DiseaseRankingTopTableDto
            {
                Name = "PUBLIC HEALTH",
                Colour = "#FD99CC",
                Rows = [new DiseaseRankingTopRowDto { Disease = "Anthrax <b>(Cattle)</b>", Rank = "1=", Score = 20, Pmr = 4, IsExotic = true }]
            }
        ],
        Charts =
        [
            new DiseaseRankingChartDto
            {
                Title = "Exotic diseases ranked according to potential impact on Public Health",
                Paragraphs = ["Explanation paragraph"],
                AxisMin = -100,
                AxisMax = 100,
                AxisInterval = 50,
                Legend = [new DiseaseRankingSeriesDto { Name = "Public health", Colour = "#FD99CC" }],
                Bars =
                [
                    new DiseaseRankingBarDto
                    {
                        Label = "ANTHRAX (CATTLE)",
                        Segments =
                        [
                            new DiseaseRankingSegmentDto { Series = "Post mitigation risk x-1", Colour = "#000000", Start = -4, End = 0 },
                            new DiseaseRankingSegmentDto { Series = "Public health", Colour = "#FD99CC", Start = 0, End = 20 }
                        ]
                    }
                ]
            },
            new DiseaseRankingChartDto { Title = "Empty chart", AxisMin = 0, AxisMax = 0, AxisInterval = 50 }
        ]
    };

    [Fact]
    public async Task Get_ListsTheReportsInBothDropdownsWithAnEditFiltersLinkBesideEach()
    {
        using var factory = CreateFactory(new FakeApi());
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync(PagePath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Disease ranking report</h1>", body, StringComparison.Ordinal);
        Assert.Contains("Scored RFI graphs", body, StringComparison.Ordinal);
        Assert.Contains("Ranked RFI graphs", body, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(body, "Disease ranking report: Terrestrial diseases", RegexOptions.None, TimeSpan.FromSeconds(5)));
        Assert.Equal(2, Regex.Count(body, ">All viral diseases<", RegexOptions.None, TimeSpan.FromSeconds(5)));
        Assert.Equal(2, Regex.Count(body, "href=\"/Reports/EditDiseaseRankingFilters\"", RegexOptions.None, TimeSpan.FromSeconds(5)));
        Assert.Equal(2, Regex.Count(body, ">Select a report<", RegexOptions.None, TimeSpan.FromSeconds(5)));
        Assert.DoesNotContain("There is a problem", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_ShowsAnErrorSummary_WhenTheReportListCannotBeLoaded()
    {
        using var factory = CreateFactory(new FakeApi { OptionsException = new HttpRequestException("down") });
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync(PagePath);

        Assert.Contains("There is a problem", body, StringComparison.Ordinal);
        Assert.Contains("The list of reports could not be loaded. Try again later", body, StringComparison.Ordinal);
        Assert.Contains("<title>Error: Disease ranking report", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostScored_DownloadsTheScoredReportAsAPdf()
    {
        var api = new FakeApi();
        var pdf = new FakePdfRenderer();
        using var factory = CreateFactory(api, pdf);
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "Scored", ("ScoredReportKey", "terrestrial"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("DiseaseRankingReport_Scored_Terrestrial.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(("terrestrial", DiseaseRankingReportKind.Scored), api.LastReportRequest);
        Assert.Matches(@"^Footer title\. Generated: \d{2}/\d{2}/\d{4}\.$", pdf.LastFooter);
    }

    [Fact]
    public async Task PostRanked_DownloadsTheRankedReportAsAPdf()
    {
        var api = new FakeApi();
        using var factory = CreateFactory(api);
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "Ranked", ("RankedReportKey", "aquatic"));

        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(("aquatic", DiseaseRankingReportKind.Ranked), api.LastReportRequest);
    }

    [Fact]
    public async Task PostScored_RendersTheReportHtmlFromTheApiData()
    {
        var pdf = new FakePdfRenderer();
        using var factory = CreateFactory(new FakeApi(), pdf);
        var client = await factory.SignedInClientAsync();

        await PostAsync(client, "Scored", ("ScoredReportKey", "terrestrial"));

        var html = pdf.LastHtml!;
        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains("Issues ranked for Terrestrial Diseases", html, StringComparison.Ordinal);
        Assert.Contains("PUBLIC HEALTH", html, StringComparison.Ordinal);
        Assert.Contains("Anthrax &lt;b&gt;(Cattle)&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("1=", html, StringComparison.Ordinal);
        Assert.Contains("Exotic diseases ranked according to potential impact on Public Health", html, StringComparison.Ordinal);
        Assert.Contains("Explanation paragraph", html, StringComparison.Ordinal);
        Assert.Contains("ANTHRAX (CATTLE)", html, StringComparison.Ordinal);
        Assert.Contains("background:#FD99CC", html, StringComparison.Ordinal);
        Assert.Contains("class=\"gridline\"", html, StringComparison.Ordinal);
        Assert.Contains("There are no diseases to show on this graph.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostScored_WithNoReportSelected_ShowsAnErrorAndDoesNotDownload()
    {
        using var factory = CreateFactory(new FakeApi());
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "Scored", ("ScoredReportKey", string.Empty));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("There is a problem", body, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#scored-report\">Select a report</a>", body, StringComparison.Ordinal);
        Assert.Contains("govuk-error-message", body, StringComparison.Ordinal);
        Assert.Contains("<title>Error: Disease ranking report", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<a href=\"#ranked-report\">", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostRanked_WithNoReportSelected_LinksTheErrorToTheRankedDropdown()
    {
        using var factory = CreateFactory(new FakeApi());
        var client = await factory.SignedInClientAsync();

        var body = await (await PostAsync(client, "Ranked", ("RankedReportKey", string.Empty))).Content.ReadAsStringAsync();

        Assert.Contains("<a href=\"#ranked-report\">Select a report</a>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostScored_WithAnUnknownReport_ShowsAnErrorAndKeepsTheSelection()
    {
        using var factory = CreateFactory(new FakeApi { ReturnsReport = false });
        var client = await factory.SignedInClientAsync();

        var body = await (await PostAsync(client, "Scored", ("ScoredReportKey", "aquatic"))).Content.ReadAsStringAsync();

        Assert.Contains("Select a report from the list", body, StringComparison.Ordinal);
        Assert.Contains("<option value=\"aquatic\" selected=\"selected\">", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostScored_WhenThePdfCannotBeGenerated_ShowsAnError()
    {
        using var factory = CreateFactory(new FakeApi(), new FakePdfRenderer { Exception = new InvalidOperationException("no browser") });
        var client = await factory.SignedInClientAsync();

        var body = await (await PostAsync(client, "Scored", ("ScoredReportKey", "terrestrial"))).Content.ReadAsStringAsync();

        Assert.Contains("The report could not be generated. Try again later", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditFiltersPage_Loads()
    {
        using var factory = CreateFactory(new FakeApi());
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync("/Reports/EditDiseaseRankingFilters");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Edit disease ranking filters", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RazorViewRenderer_Throws_WhenTheViewDoesNotExist()
    {
        using var factory = CreateFactory(new FakeApi());
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewToStringRenderer>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderAsync("~/Features/Nowhere/Missing.cshtml", new object()));

        Assert.Contains("Missing.cshtml", exception.Message, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string handler, (string Name, string Value) field)
    {
        var token = Regex.Match(
            await client.GetStringAsync(PagePath),
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)).Groups[1].Value;

        return await client.PostAsync(
            $"{PagePath}?handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                [field.Name] = field.Value
            }));
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeApi api, FakePdfRenderer? pdf = null) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDiseaseRankingApiService>();
                services.AddSingleton<IDiseaseRankingApiService>(api);
                services.RemoveAll<IPdfRenderer>();
                services.AddSingleton<IPdfRenderer>(pdf ?? new FakePdfRenderer());
            }));

    private sealed class FakeApi : IDiseaseRankingApiService
    {
        public bool ReturnsReport { get; init; } = true;

        public Exception? OptionsException { get; init; }

        public (string Key, DiseaseRankingReportKind Kind)? LastReportRequest { get; private set; }

        public Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken = default) =>
            OptionsException is null ? Task.FromResult(Options) : Task.FromException<IReadOnlyList<DiseaseRankingReportOptionDto>>(OptionsException);

        public Task<DiseaseRankingReportDto?> GetReportAsync(string reportKey, DiseaseRankingReportKind kind, CancellationToken cancellationToken = default)
        {
            LastReportRequest = (reportKey, kind);

            return Task.FromResult(ReturnsReport ? SampleReport(kind) : null);
        }
    }

    private sealed class FakePdfRenderer : IPdfRenderer
    {
        public Exception? Exception { get; init; }

        public string? LastHtml { get; private set; }

        public string? LastFooter { get; private set; }

        public Task<byte[]> RenderAsync(string html, string footerText, CancellationToken cancellationToken)
        {
            LastHtml = html;
            LastFooter = footerText;

            return Exception is null ? Task.FromResult(PdfBytes) : Task.FromException<byte[]>(Exception);
        }
    }
}
