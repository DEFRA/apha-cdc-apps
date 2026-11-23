using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

// The browser security headers, HSTS and HTTPS redirection as Program.cs wires them into the real pipeline.
public partial class SecurityHeadersTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public SecurityHeadersTests(CdcWebTestFactory factory)
    {
        WebTestEnvironment.EnsureConfigured();
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/css/site.css")]
    [InlineData("/js/govuk-frontend-6.5.0.min.js")]
    [InlineData("/health")]
    [InlineData("/no-such-page")]
    public async Task EveryResponse_CarriesTheBrowserSecurityHeaders(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Equal("geolocation=(), microphone=(), camera=()", Header(response, "Permissions-Policy"));
        Assert.Contains("frame-ancestors 'none'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePolicy_AllowsOnlyThisSiteAndTheSignInProviders()
    {
        var policy = Header(await _factory.CreateClient().GetAsync("/"), "Content-Security-Policy");

        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("style-src 'self';", policy, StringComparison.Ordinal);
        Assert.Contains("img-src 'self' data:", policy, StringComparison.Ordinal);
        Assert.Contains("font-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("form-action 'self' https://cidm.test https://login.microsoftonline.com;", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);
    }

    // The import map, the js-enabled class and GOV.UK Frontend's initAll() are the layout's inline scripts; without the nonce
    // the browser would refuse them and the GOV.UK components would never start. (The test host has no static asset
    // manifest, so the import map tag helper writes nothing here.)
    [Fact]
    public async Task EveryInlineScript_CarriesThisResponsesNonce()
    {
        var response = await _factory.CreateClient().GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        var nonce = NonceIn(Header(response, "Content-Security-Policy"));

        var inlineScripts = InlineScript().Matches(html).Select(match => match.Value).ToList();

        Assert.True(inlineScripts.Count >= 2, $"Expected the layout's inline scripts, found {inlineScripts.Count}.");
        Assert.All(inlineScripts, script => Assert.Contains($"nonce=\"{nonce}\"", script, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EachResponse_HasItsOwnNonce()
    {
        var client = _factory.CreateClient();

        var first = NonceIn(Header(await client.GetAsync("/"), "Content-Security-Policy"));
        var second = NonceIn(Header(await client.GetAsync("/"), "Content-Security-Policy"));

        Assert.NotEqual(first, second);
        Assert.Equal(22, first.Length);
    }

    [Fact]
    public async Task InDevelopment_NoHstsIsSent()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://d2r2.test") });

        var response = await client.GetAsync("/");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InProduction_AnHttpsResponseCarriesHstsAndUpgradesInsecureRequests()
    {
        await using var production = Production();
        var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://d2r2.test") });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("max-age=31536000; includeSubDomains", Header(response, "Strict-Transport-Security"));
        Assert.EndsWith("; upgrade-insecure-requests", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
    }

    // HSTS only means anything over HTTPS, so a plain-HTTP response gets neither it nor the upgrade.
    [Fact]
    public async Task InProduction_WithNoHttpsPort_APlainHttpResponseCarriesNoHstsAndNoUpgrade()
    {
        await using var production = Production(httpsPort: string.Empty);
        var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://d2r2.test") });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.DoesNotContain("upgrade-insecure-requests", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InProduction_APlainHttpRequestIsRedirectedPermanentlyToTheDefaultHttpsPort()
    {
        await using var production = Production();
        var client = production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://d2r2.test"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/HelpSupport/QualityStatement?x=1");

        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal("https://d2r2.test/HelpSupport/QualityStatement?x=1", response.Headers.Location!.ToString());
    }

    // The ALB and ECS health checks call the container over plain HTTP, so they must never be redirected.
    [Theory]
    [InlineData("/health")]
    [InlineData("/HEALTH")]
    public async Task InProduction_TheHealthCheckIsNotRedirected(string url)
    {
        await using var production = Production();
        var client = production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://d2r2.test"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private WebApplicationFactory<Program> Production(string? httpsPort = null) => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Production");

        if (httpsPort is not null)
        {
            builder.UseSetting("https_port", httpsPort);
        }
    });

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : string.Empty;

    private static string NonceIn(string policy)
    {
        var match = Nonce().Match(policy);
        Assert.True(match.Success, $"No nonce in '{policy}'.");
        return match.Groups[1].Value;
    }

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex Nonce();

    [GeneratedRegex(@"<script\b(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();
}
