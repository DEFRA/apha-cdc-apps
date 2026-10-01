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
    public async Task DocumentHistoryPage_RendersPreviousVersions()
    {
        IReadOnlyList<StaticReportVersionDto> history =
        [
            CurrentManuals[0],
            CurrentManuals[0] with
            {
                Id = Guid.NewGuid(),
                VersionMajor = 0,
                EffectiveDateFrom = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                IsCurrent = false
            }
        ];
        using var factory = CreateFactory(new FakeStaticReportsApiService(CurrentManuals, history: history));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/HelpSupport/DocumentHistory?staticReportId={StaticReportId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Previous versions", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Effective date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Version", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteConfirmation_RemovesDocumentAndRefreshesList()
    {
        var fakeService = new FakeStaticReportsApiService(CurrentManuals);
        using var factory = CreateFactory(fakeService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var initialResponse = await client.GetAsync("/HelpSupport/HelpUsingD2R2");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        var initialHtml = await initialResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            initialHtml,
            "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        Assert.True(tokenMatch.Success, "Expected anti-forgery token in the page markup.");

        var deleteResponse = await client.PostAsync(
            "/HelpSupport/HelpUsingD2R2?handler=Delete",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("documentId", VersionId.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value)
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
