using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

public class CompareVersionsIntegrationTests
{
    private const string PagePath = "/DiseaseProfiles/CompareVersions";

    private static readonly Guid ProfileId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid EarlierId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LaterId = Guid.Parse("11111111-1111-1111-1111-222222222222");
    private static readonly Guid DraftId = Guid.Parse("11111111-1111-1111-1111-333333333333");
    private static readonly Guid SectionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid QuestionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private static readonly IReadOnlyList<ProfileSearchResultDto> Profiles =
    [
        new ProfileSearchResultDto
        {
            Id = ProfileId,
            Title = "Anthrax",
            Status = "Published",
            CreatedAtUtc = new DateTime(2023, 1, 1),
            ModifiedAtUtc = new DateTime(2024, 6, 1),
            IsPublic = false,
            AffectedSpecies = [],
            PublishedVersions =
            [
                new ProfileHistoryItemDto { VersionId = EarlierId, VersionNumber = 1, Title = "Anthrax", CreatedAtUtc = new DateTime(2023, 1, 1), EffectiveToUtc = new DateTime(2024, 1, 1), IsScenario = false },
                new ProfileHistoryItemDto { VersionId = LaterId, VersionNumber = 2, Title = "Anthrax", CreatedAtUtc = new DateTime(2024, 1, 1), IsScenario = false }
            ],
            DraftVersions = [new ProfileHistoryItemDto { VersionId = DraftId, VersionNumber = 3, Title = "Anthrax", CreatedAtUtc = new DateTime(2025, 1, 1), IsScenario = false }],
            WhatIfScenarios = []
        }
    ];

    private static WebApplicationFactory<Program> CreateFactory(CapturingPdfRenderer pdf) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new FakeApiClient(Profiles));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(
                    metadata: new ProfileQuestionnaireMetadataDto
                    {
                        Sections =
                        [
                            new ProfileSectionMetadataDto
                            {
                                Id = SectionId,
                                Name = "Epidemiology",
                                SectionNumber = 1,
                                Questions =
                                [
                                    new ProfileQuestionMetadataDto
                                    {
                                        Id = QuestionId,
                                        SectionId = SectionId,
                                        QuestionNumber = 1,
                                        Fields = [new ProfileFieldMetadataDto { Id = Guid.NewGuid(), QuestionId = QuestionId, Name = "Summary", FieldNumber = 1, DataTypeName = "Text" }]
                                    }
                                ]
                            }
                        ]
                    },
                    answersByVersion: new Dictionary<Guid, ProfileSectionAnswersDto>
                    {
                        [EarlierId] = new() { FieldValues = [new ProfileFieldValueDto { QuestionId = QuestionId, FieldNumber = 1, TextValue = "Old text" }] },
                        [LaterId] = new() { FieldValues = [new ProfileFieldValueDto { QuestionId = QuestionId, FieldNumber = 1, TextValue = "New text" }] }
                    }));
                services.RemoveAll<IPdfRenderer>();
                services.AddSingleton<IPdfRenderer>(pdf);
            }));

    [Fact]
    public async Task Get_RendersInstructionsTreeAndControls()
    {
        using var factory = CreateFactory(new CapturingPdfRenderer());
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync(PagePath);

        Assert.Contains("Only two versions can be compared", Normalise(body), StringComparison.Ordinal);
        Assert.Contains("select the earlier version first", Normalise(body), StringComparison.Ordinal);
        Assert.Contains("Version 2.0, Published, effective from 01/01/2024", body, StringComparison.Ordinal);
        Assert.Contains("Version 1.0, Published, effective 01/01/2023 to 01/01/2024", body, StringComparison.Ordinal);
        Assert.Contains("Open all", body, StringComparison.Ordinal);
        Assert.Contains("Selected versions", body, StringComparison.Ordinal);
        Assert.Contains("Remove selection", body, StringComparison.Ordinal);
        Assert.Contains(">Compare</button>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_WithTwoVersions_ReturnsDifferencesOnlyPdfEarlierFirst()
    {
        var pdf = new CapturingPdfRenderer();
        using var factory = CreateFactory(pdf);
        var client = await factory.SignedInClientAsync();

        var response = await Post(client, EarlierId, LaterId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("<span class=\"removed\">Old</span> text", pdf.Html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"added\">New</span> text", pdf.Html, StringComparison.Ordinal);
        Assert.True(pdf.Html.IndexOf("Version 1.0", StringComparison.Ordinal) < pdf.Html.IndexOf("Version 2.0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Post_HonoursScriptedSelectionOrder()
    {
        var pdf = new CapturingPdfRenderer();
        using var factory = CreateFactory(pdf);
        var client = await factory.SignedInClientAsync();

        // Document order is earlier then later, but the user ticked the later version first.
        await Post(client, EarlierId, LaterId, first: LaterId, second: EarlierId);

        Assert.True(pdf.Html.IndexOf("Version 2.0", StringComparison.Ordinal) < pdf.Html.IndexOf("Version 1.0", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Post_WithoutExactlyTwoVersions_ShowsErrorAndNoPdf(int count)
    {
        using var factory = CreateFactory(new CapturingPdfRenderer());
        var client = await factory.SignedInClientAsync();
        var ids = new[] { EarlierId, LaterId, DraftId }.Take(count).ToArray();

        var response = await Post(client, ids);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("There is a problem", body, StringComparison.Ordinal);
        Assert.Contains("Select two profile versions to compare", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_WhenRenderingFails_ShowsError()
    {
        using var factory = CreateFactory(new CapturingPdfRenderer { Failure = new HttpRequestException("down") });
        var client = await factory.SignedInClientAsync();

        var body = await (await Post(client, EarlierId, LaterId)).Content.ReadAsStringAsync();

        Assert.Contains("could not be generated", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveSelection_RedirectsToClearedPage()
    {
        using var factory = CreateFactory(new CapturingPdfRenderer());
        var client = await factory.SignedInClientAsync();
        var token = ExtractToken(await client.GetStringAsync(PagePath));

        var response = await client.PostAsync(
            $"{PagePath}?handler=RemoveSelection",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["SelectedVersions"] = EarlierId.ToString()
            }));

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found);
        Assert.DoesNotContain("checked=\"checked\"", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, params Guid[] selected) =>
        await Post(client, selected, null, null);

    private static async Task<HttpResponseMessage> Post(HttpClient client, Guid a, Guid b, Guid first, Guid second) =>
        await Post(client, [a, b], first, second);

    private static async Task<HttpResponseMessage> Post(HttpClient client, Guid[] selected, Guid? first, Guid? second)
    {
        var token = ExtractToken(await client.GetStringAsync(PagePath));
        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        form.AddRange(selected.Select(id => new KeyValuePair<string, string>("SelectedVersions", id.ToString())));

        if (first is not null && second is not null)
        {
            form.Add(new("FirstVersionId", first.ToString()!));
            form.Add(new("SecondVersionId", second.ToString()!));
        }

        return await client.PostAsync(PagePath, new FormUrlEncodedContent(form));
    }

    private static string Normalise(string html) => Regex.Replace(html, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static string ExtractToken(string html)
    {
        var match = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(match.Success, "No antiforgery token found on the page.");

        return match.Groups[1].Value;
    }

    private sealed class CapturingPdfRenderer : IPdfRenderer
    {
        public string Html { get; private set; } = string.Empty;

        public Exception? Failure { get; init; }

        public Task<byte[]> RenderLandscapeAsync(string html, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromException<byte[]>(Failure);
            }

            Html = html;

            return Task.FromResult<byte[]>([0x25, 0x50, 0x44, 0x46]);
        }
    }
}
