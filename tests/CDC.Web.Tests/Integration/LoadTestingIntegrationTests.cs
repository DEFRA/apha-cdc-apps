using System.Globalization;
using System.Net;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Events;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// LoadTesting end to end through the real pipeline: the landing page's two sign-in links, a page that needs a user, sign out, and
// the window closing. The CDC.Api-backed resolvers are replaced, so the user's row and roles are whatever the test sets.
public class LoadTestingIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private const string Banner = "Load testing is on";

    private readonly CdcWebTestFactory _factory;

    public LoadTestingIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> Factory(
        FakeIdentityResolvers resolvers,
        ControllableClock? clock = null,
        string? deployment = "TEST",
        TimeSpan? window = null)
    {
        var appClock = clock ?? new ControllableClock(DateTimeOffset.UtcNow);
        var expiresAtUtc = appClock.GetUtcNow().Add(window ?? TimeSpan.FromHours(1)).ToString("o", CultureInfo.InvariantCulture);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("LoadTesting:Enabled", "true");
            builder.UseSetting("LoadTesting:ExpiresAtUtc", expiresAtUtc);
            builder.UseSetting("LoadTesting:InternalUserName", "internal.user");
            builder.UseSetting("LoadTesting:InternalSsoUserId", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            builder.UseSetting("LoadTesting:ExternalUserEmail", "external.user@example.test");
            builder.UseSetting("LoadTesting:ExternalSsoUserId", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            if (deployment is not null)
            {
                builder.UseSetting("Deployment:Environment", deployment);
            }

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(appClock);
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => options.TimeProvider = appClock);
                services.PostConfigure<CookieAuthenticationOptions>(EntraAuthenticationDefaults.CookieAuthenticationScheme, options => options.TimeProvider = appClock);
                services.RemoveAll<IEntraInternalUserResolver>();
                services.RemoveAll<ICidmExternalUserResolver>();
                services.AddSingleton<IEntraInternalUserResolver>(resolvers);
                services.AddSingleton<ICidmExternalUserResolver>(resolvers);
            });
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    [Fact]
    public async Task LoginInternal_SignsInTheConfiguredInternalUserWithoutTheIdentityProvider()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);

        var login = await client.GetAsync("/Account/LoginInternal");
        var home = await client.GetStringAsync("/Landing/Internal");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Landing/Internal", login.Headers.Location!.OriginalString);
        Assert.Contains("Load Test Internal", home, StringComparison.Ordinal);
        Assert.Contains(Banner, home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginInternal_GivesTheRoleThePermissionFlagsOfTheUsersRecordSay()
    {
        var resolvers = new FakeIdentityResolvers();
        using var factory = Factory(resolvers);
        var editor = Client(factory);
        await editor.GetAsync("/Account/LoginInternal");

        resolvers.InternalResolution = EntraInternalUserResolution.Allow(new Dictionary<string, string>
        {
            ["internalUserId"] = "33333333-3333-3333-3333-333333333333",
            ["fullName"] = "Load Test Reader",
            ["authenticationProvider"] = "entra",
            ["isProfileEditor"] = bool.FalseString,
            ["isPolicyProfileUser"] = bool.FalseString
        });
        var reader = Client(factory);
        await reader.GetAsync("/Account/LoginInternal");

        Assert.Contains("Create profile", await editor.GetStringAsync("/Landing/Internal"), StringComparison.Ordinal);
        Assert.DoesNotContain("Create profile", await reader.GetStringAsync("/Landing/Internal"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_SignsInTheConfiguredExternalUserWithoutTheIdentityProvider()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);

        var login = await client.GetAsync("/Account/Login");
        var home = await client.GetStringAsync("/Landing/External");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Landing/External", login.Headers.Location!.OriginalString);
        Assert.Contains("Load Test External", home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUserSignedInOneWay_CannotSignInTheOtherWayInTheSameBrowser()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);
        await client.GetAsync("/Account/LoginInternal");

        var external = await client.GetAsync("/Account/Login");

        Assert.Equal("/Account/AlreadySignedIn", external.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task APageThatNeedsAnInternalUser_SendsAnAnonymousRequestToTheAppsLoginAndBackNotToEntra()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);

        var challenge = await client.GetAsync("/Landing/Internal");
        var login = await client.GetAsync(challenge.Headers.Location!.PathAndQuery);
        var page = await client.GetAsync(login.Headers.Location!.OriginalString);

        Assert.Equal("/Account/LoginInternal", challenge.Headers.Location.AbsolutePath);
        Assert.Equal("/Landing/Internal", login.Headers.Location.OriginalString);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task APageThatNeedsAnExternalUser_SendsAnAnonymousRequestToTheAppsExternalLoginNotToCidm()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);

        var challenge = await client.GetAsync("/Landing/External");
        var login = await client.GetAsync(challenge.Headers.Location!.PathAndQuery);
        var page = await client.GetAsync(login.Headers.Location!.OriginalString);

        Assert.Equal("/Account/Login", challenge.Headers.Location.AbsolutePath);
        Assert.Equal("/Landing/External", login.Headers.Location.OriginalString);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task WhenTheResolverRefusesTheUser_NoSessionIsCreatedAndTheUserIsSentWhereTheResolverSays()
    {
        var resolvers = new FakeIdentityResolvers { InternalResolution = EntraInternalUserResolution.Deny("/Account/NotPermitted") };
        using var factory = Factory(resolvers);
        var client = Client(factory);

        var login = await client.GetAsync("/Account/LoginInternal");
        var home = await client.GetAsync("/Landing/Internal");

        Assert.Equal("/Account/NotPermitted", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
    }

    [Fact]
    public async Task LogoutInternal_EndsTheSessionWithoutGoingToEntra()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);
        await client.GetAsync("/Account/LoginInternal");

        var logout = await client.GetAsync("/Account/LogoutInternal");
        var home = await client.GetAsync("/Landing/Internal");

        Assert.Equal("/Account/SignedOut", logout.Headers.Location!.OriginalString);
        Assert.Equal("/Account/LoginInternal", home.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Logout_EndsTheSessionWithoutGoingToCidm()
    {
        using var factory = Factory(new FakeIdentityResolvers());
        var client = Client(factory);
        await client.GetAsync("/Account/Login");

        var logout = await client.GetAsync("/Account/Logout");
        var home = await client.GetAsync("/Landing/External");

        Assert.Equal("/Account/SignedOut", logout.Headers.Location!.OriginalString);
        Assert.Equal("/Account/Login", home.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task ThePublicLandingPage_WarnsThatLoadTestingIsOn()
    {
        using var factory = Factory(new FakeIdentityResolvers());

        var page = await Client(factory).GetStringAsync("/");

        Assert.Contains(Banner, page, StringComparison.Ordinal);
        Assert.Contains("It switches itself off at", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterTheWindowEnds_ASessionKeptAliveByActivityIsEndedAndSignInGoesToTheIdentityProviderAgain()
    {
        var clock = new ControllableClock(DateTimeOffset.UtcNow);
        using var factory = Factory(new FakeIdentityResolvers(), clock, window: TimeSpan.FromMinutes(10));
        var client = Client(factory);
        await client.GetAsync("/Account/LoginInternal");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/Internal")).StatusCode);

        // Past the window but inside the 20-minute idle timeout, so only the end of the window can have stopped the session.
        clock.Advance(TimeSpan.FromMinutes(15));
        var ended = await client.GetAsync("/Landing/Internal");
        var challenge = await Client(factory).GetAsync("/Landing/Internal");
        var login = await Client(factory).GetAsync("/Account/LoginInternal");

        Assert.Equal("/", ended.Headers.Location!.OriginalString);
        Assert.Contains(ended.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith(".AspNetCore.Cookies.Entra=;", StringComparison.Ordinal));
        Assert.Equal("/Account/LoginInternal", challenge.Headers.Location!.AbsolutePath);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, login.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterTheWindowEnds_ExternalSignInGoesToCidmAgain()
    {
        var clock = new ControllableClock(DateTimeOffset.UtcNow);
        using var factory = Factory(new FakeIdentityResolvers(), clock, window: TimeSpan.FromMinutes(10));
        clock.Advance(TimeSpan.FromMinutes(15));

        var login = await Client(factory).GetAsync("/Account/Login");

        Assert.StartsWith(CdcWebTestFactory.FakeAuthorizationEndpoint, login.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenOff_ARequestForAPageThatNeedsAUserStillGoesStraightToTheIdentityProvider()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var response = await client.GetAsync("/Landing/Internal");
        var landing = await client.GetStringAsync("/");

        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Banner, landing, StringComparison.Ordinal);
    }

    [Fact]
    public void InProduction_TheAppRefusesToStart()
    {
        using var factory = Factory(new FakeIdentityResolvers(), deployment: "PROD");

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("'PROD'", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WithAWindowOverAWeek_TheAppRefusesToStart()
    {
        using var factory = Factory(new FakeIdentityResolvers(), window: TimeSpan.FromDays(8));

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("7 days", failure.ToString(), StringComparison.Ordinal);
    }
}
