using System.Net;
using System.Net.Http.Json;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Integration;

// The session timeout end to end: how long a signed-in session lasts, what extends it, and what a request does once it has ended.
// A controllable clock stands in for the passing of time, which both sign-in cookies and the app's own code read.
public class SessionTimeoutIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private const string StatusPath = "/Session/Status";
    private const string ExtendPath = "/Session/Extend";
    private const string AuthCookiePrefix = ".AspNetCore.Cookies";
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(20);

    private readonly CdcWebTestFactory _factory;

    public SessionTimeoutIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    private static WebApplicationFactory<Program> WithClock(WebApplicationFactory<Program> factory, ControllableTimeProvider clock, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => options.TimeProvider = clock);
                services.PostConfigure<CookieAuthenticationOptions>(EntraAuthenticationDefaults.CookieAuthenticationScheme, options => options.TimeProvider = clock);
            });
        });

    private static async Task<SessionStatus> StatusAsync(HttpClient client)
    {
        var response = await client.GetAsync(StatusPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<SessionStatus>())!;
    }

    // Whether the response carries a new sign-in cookie, which is how a session is renewed.
    private static bool RenewsTheSession(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(cookie => cookie.StartsWith(AuthCookiePrefix, StringComparison.Ordinal));

    private static HttpRequestMessage ExtendRequest(SessionStatus status)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ExtendPath);
        request.Headers.Add(status.AntiforgeryHeaderName!, status.AntiforgeryToken);

        return request;
    }

    [Fact]
    public async Task ARequestIsActivityThatRenewsTheSession_ButAskingHowLongItHasLeftIsNot()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInClientAsync();

        clock.Advance(TimeSpan.FromMinutes(10));
        var firstCheck = await client.GetAsync(StatusPath);
        Assert.Equal(10 * 60, (await firstCheck.Content.ReadFromJsonAsync<SessionStatus>())!.RemainingSeconds);
        Assert.False(RenewsTheSession(firstCheck));

        // Had the first check extended the session it would have a full timeout left now, not the one minute it had.
        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(60, (await StatusAsync(client)).RemainingSeconds);

        var page = await client.GetAsync("/Landing/Internal");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(RenewsTheSession(page));
        Assert.Equal((int)IdleTimeout.TotalSeconds, (await StatusAsync(client)).RemainingSeconds);
    }

    [Fact]
    public async Task ASessionIsIdleForItsTimeoutNotLivedForIt()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInInternalClientAsync();

        // Each request starts the timeout afresh, even one made less than halfway through the last: the built-in sliding expiration
        // would not renew at 9 minutes, and so would end the session at 20, only 11 minutes after that request.
        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/Internal")).StatusCode);

        // Each of these is 19 minutes after the last request, so within 20 minutes of it though far longer after signing in.
        clock.Advance(TimeSpan.FromMinutes(19));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/Internal")).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(19));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/Internal")).StatusCode);
    }

    [Fact]
    public async Task ARequestSoonAfterTheLastRenewalDoesNotReissueTheCookie_ButOneAMinuteLaterDoes()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInClientAsync();

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(RenewsTheSession(await client.GetAsync("/Landing/Internal")));

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(RenewsTheSession(await client.GetAsync("/Landing/Internal")));
    }

    [Fact]
    public async Task AnInternalSessionIdleForLongerThanTheTimeoutIsChallengedByEntraIdRatherThanFailingSilently()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInInternalClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/Internal")).StatusCode);

        clock.Advance(IdleTimeout + TimeSpan.FromSeconds(1));

        Assert.False((await StatusAsync(client)).Authenticated);
        var response = await client.GetAsync("/Landing/Internal");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AnExternalSessionIdleForLongerThanTheTimeoutIsChallengedByCidm_AndSigningInAgainGoesThereToo()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInExternalClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/External")).StatusCode);

        clock.Advance(IdleTimeout + TimeSpan.FromSeconds(1));

        var page = await client.GetAsync("/Landing/External");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeAuthorizationEndpoint, page.Headers.Location!.ToString());

        var signInAgain = await client.GetAsync($"/Session/SignInAgain?returnUrl={Uri.EscapeDataString("/Landing/External")}");
        Assert.Equal(HttpStatusCode.Redirect, signInAgain.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeAuthorizationEndpoint, signInAgain.Headers.Location!.ToString());
    }

    [Fact]
    public async Task SigningInAgainFromAnInternalSessionThatHasEndedGoesToEntraId()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInInternalClientAsync();
        clock.Advance(IdleTimeout + TimeSpan.FromSeconds(1));

        var response = await client.GetAsync($"/Session/SignInAgain?returnUrl={Uri.EscapeDataString("/SurveillanceProfiles/Search")}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ExtendingTheSessionGivesItAFullTimeoutFromNow()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInClientAsync();
        clock.Advance(TimeSpan.FromMinutes(16));
        var status = await StatusAsync(client);
        Assert.Equal(4 * 60, status.RemainingSeconds);

        using var request = ExtendRequest(status);
        var extended = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        Assert.True(RenewsTheSession(extended));
        Assert.Equal(new SessionStatus(true, (int)IdleTimeout.TotalSeconds), await extended.Content.ReadFromJsonAsync<SessionStatus>());

        // Past the 20 minutes the session had before it was extended.
        clock.Advance(TimeSpan.FromMinutes(19));
        Assert.Equal(60, (await StatusAsync(client)).RemainingSeconds);
    }

    [Fact]
    public async Task ExtendingTheSessionNeedsTheTokenTheStatusCheckGave()
    {
        var client = await _factory.SignedInClientAsync();

        var response = await client.PostAsync(ExtendPath, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ASessionThatHasEndedCannotBeExtended_AndIsNotRevivedByTrying()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(_factory, clock);
        var client = await factory.SignedInClientAsync();
        var status = await StatusAsync(client);
        clock.Advance(IdleTimeout + TimeSpan.FromSeconds(1));

        using var request = ExtendRequest(status);
        var response = await client.SendAsync(request);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized });
        Assert.False((await StatusAsync(client)).Authenticated);
    }

    [Fact]
    public async Task WithNoSessionTheStatusCheckSaysSoRatherThanRedirectingToSignIn()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(StatusPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SessionStatus.Ended, await response.Content.ReadFromJsonAsync<SessionStatus>());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task WithNoSessionExtendingIsRefusedRatherThanRedirectedToSignIn()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync(ExtendPath, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheStatusCheckGivesAFreshTokenAndNeverCachesTheAnswer()
    {
        var client = await _factory.SignedInClientAsync();

        var response = await client.GetAsync(StatusPath);

        Assert.True(response.Headers.CacheControl!.NoStore);
        var status = (await response.Content.ReadFromJsonAsync<SessionStatus>())!;
        Assert.True(status.Authenticated);
        Assert.False(string.IsNullOrEmpty(status.AntiforgeryToken));
        Assert.False(string.IsNullOrEmpty(status.AntiforgeryHeaderName));
    }

    [Fact]
    public async Task SigningInAgainNeverReturnsAUserToAnotherSite()
    {
        var client = await _factory.SignedInInternalClientAsync();

        var elsewhere = await client.GetAsync($"/Session/SignInAgain?returnUrl={Uri.EscapeDataString("https://elsewhere.example/phish")}");
        var local = await client.GetAsync($"/Session/SignInAgain?returnUrl={Uri.EscapeDataString("/SurveillanceProfiles/Search?q=anthrax")}");

        Assert.Equal("/Landing/Internal", elsewhere.Headers.Location!.OriginalString);
        Assert.Equal("/SurveillanceProfiles/Search?q=anthrax", local.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task EverySignedInPageCarriesTheTimeoutDialogsAndTheScriptThatOpensThem()
    {
        var client = await _factory.SignedInClientAsync();

        var html = await client.GetStringAsync("/Landing/Internal");

        Assert.Contains("data-session-timeout", html);
        Assert.Contains("data-status-url=\"/Session/Status\"", html);
        Assert.Contains("data-extend-url=\"/Session/Extend\"", html);
        Assert.Contains("data-idle-seconds=\"1200\"", html);
        Assert.Contains("data-warning-seconds=\"300\"", html);
        Assert.Contains("id=\"session-warning-dialog\"", html);
        Assert.Contains("Extend session?", html);
        Assert.Contains("id=\"session-expired-dialog\"", html);
        Assert.Contains("Session expired", html);
        Assert.Contains("/js/session-timeout.js", html);
    }

    [Fact]
    public async Task SigningInAgainBringsTheUserBackToThePageTheyWereOn()
    {
        var client = await _factory.SignedInClientAsync();

        var html = await client.GetStringAsync("/Landing/Internal?page=2");

        Assert.Contains("method=\"get\" action=\"/Session/SignInAgain\"", html);
        Assert.Contains("name=\"returnUrl\" value=\"/Landing/Internal?page=2\"", html);
    }

    // A page that answers a form post cannot be asked for again with a plain request, so there is nothing to return the user to.
    [Fact]
    public async Task APageThatAnsweredAFormPostHasNoPageToReturnTheUserTo()
    {
        var client = await _factory.SignedInClientAsync();

        var response = await client.PostAsync("/Landing/Internal", null);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"returnUrl\" />", html);
        Assert.DoesNotContain("name=\"returnUrl\" value=", html);
    }

    [Fact]
    public async Task ThePageTellsTheScriptTheConfiguredTimeouts()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("SessionTimeout:IdleTimeout", "00:30:00")
            .UseSetting("SessionTimeout:WarningBeforeExpiry", "00:02:30"));
        var client = await factory.SignedInClientAsync();

        var html = await client.GetStringAsync("/Landing/Internal");

        Assert.Contains("data-idle-seconds=\"1800\"", html);
        Assert.Contains("data-warning-seconds=\"150\"", html);
    }

    // Before sign-in there is no session to warn about, and the script would only be asking the server about nothing.
    [Fact]
    public async Task APageSeenWithoutASessionHasNeitherTheDialogsNorTheScript()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Account/SignedOut");

        Assert.DoesNotContain("data-session-timeout", html);
        Assert.DoesNotContain("session-warning-dialog", html);
        Assert.DoesNotContain("session-timeout.js", html);
    }

    [Fact]
    public void BothSignInCookiesLastForTheIdleTimeoutAndSlideWithActivity()
    {
        var cookies = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        foreach (var scheme in new[] { CookieAuthenticationDefaults.AuthenticationScheme, EntraAuthenticationDefaults.CookieAuthenticationScheme })
        {
            Assert.Equal(IdleTimeout, cookies.Get(scheme).ExpireTimeSpan);
            Assert.True(cookies.Get(scheme).SlidingExpiration);
        }
    }

    [Fact]
    public void ConfiguringTheIdleTimeoutChangesTheLifetimeOfBothSignInCookies()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("SessionTimeout:IdleTimeout", "00:10:00"));

        var cookies = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        Assert.Equal(TimeSpan.FromMinutes(10), cookies.Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan);
        Assert.Equal(TimeSpan.FromMinutes(10), cookies.Get(EntraAuthenticationDefaults.CookieAuthenticationScheme).ExpireTimeSpan);
    }

    [Fact]
    public void AnInvalidSessionTimeoutStopsTheAppStartingRatherThanLettingSessionsEndUnwarned()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("SessionTimeout:WarningBeforeExpiry", "00:00:05"));

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("WarningBeforeExpiry", failure.ToString());
    }

    private sealed class ControllableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
