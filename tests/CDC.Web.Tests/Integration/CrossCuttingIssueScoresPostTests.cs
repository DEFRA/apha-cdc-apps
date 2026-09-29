using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

/// <summary>
/// Drives the real HTTP request pipeline so model binding runs. The page-model unit tests invoke
/// handlers directly and therefore cannot catch binding-level faults.
/// </summary>
public class CrossCuttingIssueScoresPostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PagePath = "/CrossProfileAdmin/CrossCuttingIssueScores";

    private readonly WebApplicationFactory<Program> factory;

    public CrossCuttingIssueScoresPostTests(WebApplicationFactory<Program> factory)
    {
        WebTestEnvironment.EnsureConfigured();
        this.factory = factory;
    }

    // The session cookie is marked Secure, so it is only returned over HTTPS. Browsers make an
    // exception for localhost; HttpClient does not.
    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Fact]
    public async Task Update_WithNothingSelected_DoesNotRenderBindingErrors()
    {
        var client = CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, PagePath);

        var response = await client.PostAsync(
            $"{PagePath}?handler=Update",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("There is a problem", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Unrecognized Guid format", body, StringComparison.Ordinal);
        Assert.DoesNotContain("The Scores field is required", body, StringComparison.Ordinal);
        Assert.Contains("There are no changes to save", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_WithCategorySelected_DoesNotRenderBindingErrors()
    {
        var client = CreateClient();
        var bioSecurityId = "E0C3A33E-CFAC-4033-83C0-A88AD53417B7";
        var url = $"{PagePath}?CategoryId={bioSecurityId}";
        var token = await GetAntiforgeryTokenAsync(client, url);

        var response = await client.PostAsync(
            $"{PagePath}?handler=Update",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["CategoryId"] = bioSecurityId
            }));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("There is a problem", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmendScoreThenUpdate_SavesWithoutRenderingErrors()
    {
        var client = CreateClient();
        var bioSecurityId = "E0C3A33E-CFAC-4033-83C0-A88AD53417B7";
        var livestockContactsId = "10F04FB1-FEFA-49B6-B1C0-BA204C12B1B4";

        var panelUrl = $"{PagePath}?CategoryId={bioSecurityId}&CriterionId={livestockContactsId}";
        var panelHtml = await client.GetStringAsync(panelUrl);

        var scoreInputs = Regex.Matches(
            panelHtml,
            """name="(Scores\[[^\]]+\])"[^>]*value="([^"]*)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.NotEmpty(scoreInputs);

        var applyForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ExtractToken(panelHtml),
            ["CategoryId"] = bioSecurityId,
            ["CriterionId"] = livestockContactsId
        };

        // Amend the first score; post the rest unchanged, as the browser would.
        for (var i = 0; i < scoreInputs.Count; i++)
        {
            var name = scoreInputs[i].Groups[1].Value;
            applyForm[name] = i == 0 ? "42" : scoreInputs[i].Groups[2].Value;
        }

        var applyResponse = await client.PostAsync($"{PagePath}?handler=Apply", new FormUrlEncodedContent(applyForm));
        var applyBody = await applyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);
        Assert.DoesNotContain("There is a problem", applyBody, StringComparison.Ordinal);
        Assert.Contains("You have pending changes", applyBody, StringComparison.Ordinal);

        var updateResponse = await client.PostAsync(
            $"{PagePath}?handler=Update",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ExtractToken(applyBody),
                ["CategoryId"] = bioSecurityId
            }));

        var updateBody = await updateResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.DoesNotContain("There is a problem", updateBody, StringComparison.Ordinal);
        Assert.Contains("Your changes were successfully saved", updateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("You have pending changes", updateBody, StringComparison.Ordinal);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url) =>
        ExtractToken(await client.GetStringAsync(url));

    private static string ExtractToken(string html)
    {
        var match = Regex.Match(
            html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        Assert.True(match.Success, "No antiforgery token found on the page.");

        return match.Groups[1].Value;
    }
}
