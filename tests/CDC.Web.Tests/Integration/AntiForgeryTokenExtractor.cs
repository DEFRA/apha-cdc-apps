using System.Text.RegularExpressions;

namespace CDC.Web.Tests.Integration;

// Shared helper so page-rendering integration tests can extract the hidden anti-forgery
// token before posting a form, without each test file compiling its own throwaway regex.
internal static partial class AntiForgeryTokenExtractor
{
    public static string GetToken(string html)
    {
        var match = TokenRegex().Match(html);

        Assert.True(match.Success, "Expected anti-forgery token in the page markup.");

        return match.Groups[1].Value;
    }

    // Collapses the GET-then-extract boilerplate repeated across every form-posting test.
    public static async Task<string> GetTokenFromPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        return GetToken(html);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();
}
