using System.Net;
using CDC.Auth.Entra;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Integration;

public class CookiesPageIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public CookiesPageIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ASignedOutVisitor_CanReadTheCookiesPage()
    {
        var page = await GetCookiesPageAsync();

        Assert.Contains("<h1 class=\"govuk-heading-l\">Cookies on Disease Briefing, Decisions Support, Risk Assessment &amp; Ranking</h1>", page);
        Assert.Contains("This service only uses essential cookies.", page);
        Assert.Contains("You are signed out after 20 minutes without activity.", page);
    }

    [Fact]
    public async Task TheFooterCookiesLink_GoesToTheCookiesPage()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var page = await client.GetStringAsync("/");

        Assert.Contains("<a class=\"govuk-footer__link\" href=\"/cookies\">Cookies</a>", page);
    }

    // Read from the running app's options, so the page has to change when a cookie's name does.
    [Fact]
    public async Task ThePage_ListsTheCookiesTheAppIsConfiguredToSet()
    {
        var services = _factory.Services;
        var cookieOptions = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var antiforgeryCookie = services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name!;

        var page = await GetCookiesPageAsync();

        var externalSignIn = cookieOptions.Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name;
        var internalSignIn = cookieOptions.Get(EntraAuthenticationDefaults.CookieAuthenticationScheme).Cookie.Name;
        Assert.Contains($">{externalSignIn} (and {externalSignIn}C1, {externalSignIn}C2)<", page);
        Assert.Contains($">{internalSignIn} (and {internalSignIn}C1, {internalSignIn}C2)<", page);
        Assert.Contains($">{services.GetRequiredService<IOptions<SessionOptions>>().Value.Cookie.Name}<", page);
        Assert.Contains($">{services.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name}<", page);
        Assert.StartsWith(".AspNetCore.Antiforgery.", antiforgeryCookie, StringComparison.Ordinal);
        Assert.Contains(">.AspNetCore.Antiforgery.*<", page);
    }

    // Starting either sign-in sets the two short-lived cookies the page lists, with the lifetimes it gives.
    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/LoginInternal")]
    public async Task StartingASignIn_SetsTheCorrelationAndNonceCookiesThePageLists(string signInPath)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(signInPath);
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        AssertLifetime(setCookies, ".AspNetCore.Correlation.", TimeSpan.FromMinutes(15));
        AssertLifetime(setCookies, ".AspNetCore.OpenIdConnect.Nonce.", TimeSpan.FromMinutes(15));

        var page = await GetCookiesPageAsync();
        Assert.Contains(">.AspNetCore.Correlation.* and .AspNetCore.OpenIdConnect.Nonce.*<", page);
        Assert.Contains(">15 minutes<", page);
    }

    private static void AssertLifetime(IEnumerable<string> setCookies, string namePrefix, TimeSpan expected)
    {
        var cookie = Assert.Single(setCookies, value => value.StartsWith(namePrefix, StringComparison.Ordinal));
        var expires = cookie.Split(';').Select(part => part.Trim()).Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        var lifetime = DateTimeOffset.Parse(expires["expires=".Length..], System.Globalization.CultureInfo.InvariantCulture) - DateTimeOffset.UtcNow;

        Assert.InRange(lifetime, expected - TimeSpan.FromMinutes(1), expected + TimeSpan.FromMinutes(1));
    }

    private async Task<string> GetCookiesPageAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/cookies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }
}
