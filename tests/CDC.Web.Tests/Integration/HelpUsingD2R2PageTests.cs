using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

public class HelpUsingD2R2PageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HelpUsingD2R2PageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HelpUsingD2R2Page_RendersDocumentTable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/HelpUsingD2R2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Title", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Version", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Effective date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("History", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Delete", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/files/help-using-d2r2-guidance.pdf", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentHistoryPage_RendersPreviousVersions()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/DocumentHistory?documentId=guidance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Previous versions", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Effective date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Version", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteConfirmation_RemovesDocumentAndRefreshesList()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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
                new KeyValuePair<string, string>("documentId", "guidance"),
                new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value)
            ]));

        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        Assert.Equal("/HelpSupport/HelpUsingD2R2", deleteResponse.Headers.Location?.OriginalString);

        var refreshed = await client.GetAsync(deleteResponse.Headers.Location!.OriginalString);
        var refreshedHtml = await refreshed.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.DoesNotContain("Help using D2R2 guidance", refreshedHtml, StringComparison.OrdinalIgnoreCase);
    }
}
