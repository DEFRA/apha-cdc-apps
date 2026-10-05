using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

public class HelpUsingD2R2PageTests
{
    private static readonly Guid StaticReportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlyList<StaticReportVersionDto> CurrentManuals =
    [
        new StaticReportVersionDto
        {
            Id = VersionId,
            StaticReportId = StaticReportId,
            Title = "Help using D2R2 guidance",
            VersionMajor = 1,
            EffectiveDateFrom = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
            IsCurrent = true,
            IsUserManual = true,
            IsPublic = false,
            FileSize = 1024
        }
    ];

    [Fact]
    public async Task HelpUsingD2R2Page_RendersDocumentTable()
    {
        using var factory = CreateFactory(new FakeStaticReportsApiService(CurrentManuals));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/HelpUsingD2R2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Title", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Version", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Effective date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("History", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Delete", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Help using D2R2 guidance", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HelpUsingD2R2Page_RendersPaginationControls_WhenMultiplePagesExist()
    {
        var manyManuals = Enumerable.Range(0, 15)
            .Select(index => CurrentManuals[0] with
            {
                Id = Guid.NewGuid(),
                Title = $"Help using D2R2 guidance {index}",
                VersionMajor = 1
            })
            .ToArray();
        using var factory = CreateFactory(new FakeStaticReportsApiService(manyManuals));
        var client = factory.CreateClient();

        var firstPageResponse = await client.GetAsync("/HelpSupport/HelpUsingD2R2");
        var firstPageHtml = await firstPageResponse.Content.ReadAsStringAsync();

        Assert.Contains("govuk-pagination__list", firstPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("govuk-pagination__prev", firstPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("govuk-pagination__next", firstPageHtml, StringComparison.OrdinalIgnoreCase);

        var secondPageResponse = await client.GetAsync("/HelpSupport/HelpUsingD2R2?pageNumber=2");
        var secondPageHtml = await secondPageResponse.Content.ReadAsStringAsync();

        Assert.Contains("govuk-pagination__prev", secondPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("govuk-pagination__next", secondPageHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentHistoryPage_RendersPreviousVersions()
    {
        using var factory = CreateFactory(new FakeStaticReportsApiService(CurrentManuals, history: BuildHistory()));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/HelpSupport/DocumentHistory?staticReportId={StaticReportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("History for Help using D2R2 guidance", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Effective date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Version", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentHistoryPage_DefaultsToDescendingAndLinksToAscendingSort()
    {
        using var factory = CreateFactory(new FakeStaticReportsApiService(CurrentManuals, history: BuildHistory()));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/HelpSupport/DocumentHistory?staticReportId={StaticReportId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("aria-sort=\"descending\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sortOrder=asc", html, StringComparison.Ordinal);
        Assert.True(IndexOfVersionCell(html, "1.0") < IndexOfVersionCell(html, "0.0"));
    }

    [Fact]
    public async Task DocumentHistoryPage_AscendingSortOrdersOldestVersionFirst()
    {
        using var factory = CreateFactory(new FakeStaticReportsApiService(CurrentManuals, history: BuildHistory()));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/HelpSupport/DocumentHistory?staticReportId={StaticReportId}&sortOrder=asc");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("aria-sort=\"ascending\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sortOrder=desc", html, StringComparison.Ordinal);
        Assert.True(IndexOfVersionCell(html, "0.0") < IndexOfVersionCell(html, "1.0"));
    }

    [Fact]
    public async Task DocumentHistoryPage_RendersPaginationControls_WhenMultiplePagesExist()
    {
        var history = Enumerable.Range(0, 15)
            .Select(index => CurrentManuals[0] with
            {
                Id = Guid.NewGuid(),
                VersionMajor = (byte)index,
                EffectiveDateFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                IsCurrent = index == 14
            })
            .ToArray();
        using var factory = CreateFactory(new FakeStaticReportsApiService(CurrentManuals, history: history));
        var client = factory.CreateClient();

        var firstPageResponse = await client.GetAsync($"/HelpSupport/DocumentHistory?staticReportId={StaticReportId}");
        var firstPageHtml = await firstPageResponse.Content.ReadAsStringAsync();

        Assert.Contains("govuk-pagination__list", firstPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("govuk-pagination__item--current", firstPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("govuk-pagination__prev", firstPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("govuk-pagination__next", firstPageHtml, StringComparison.OrdinalIgnoreCase);

        var secondPageResponse = await client.GetAsync($"/HelpSupport/DocumentHistory?staticReportId={StaticReportId}&pageNumber=2");
        var secondPageHtml = await secondPageResponse.Content.ReadAsStringAsync();

        Assert.Contains("govuk-pagination__prev", secondPageHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("govuk-pagination__next", secondPageHtml, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<StaticReportVersionDto> BuildHistory() =>
    [
        CurrentManuals[0],
        CurrentManuals[0] with
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            VersionMajor = 0,
            EffectiveDateFrom = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            IsCurrent = false
        }
    ];

    private static int IndexOfVersionCell(string html, string version)
    {
        var index = html.IndexOf($"<td class=\"govuk-table__cell\">{version}</td>", StringComparison.Ordinal);
        Assert.True(index >= 0, $"Expected a version cell for {version}.");
        return index;
    }

    [Fact]
    public async Task DeleteConfirmation_RemovesDocumentAndRefreshesList()
    {
        var fakeService = new FakeStaticReportsApiService(CurrentManuals);
        using var factory = CreateFactory(fakeService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var initialResponse = await client.GetAsync("/HelpSupport/HelpUsingD2R2");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        var token = await AntiForgeryTokenExtractor.GetTokenFromPageAsync(client, "/HelpSupport/HelpUsingD2R2");

        var deleteResponse = await client.PostAsync(
            "/HelpSupport/HelpUsingD2R2?handler=Delete",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("documentId", VersionId.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            ]));

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(VersionId, fakeService.DeletedStaticReportVersionId);

        var deleteHtml = await deleteResponse.Content.ReadAsStringAsync();
        Assert.Contains("successfully deleted", deleteHtml, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(IStaticReportsApiService fakeService) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaticReportsApiService>();
                services.AddSingleton(fakeService);
            }));
}
