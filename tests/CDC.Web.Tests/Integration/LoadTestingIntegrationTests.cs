using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CDC.Auth.Entra;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// LoadTesting end to end through the real pipeline: the landing page's two sign-in links, the Load test login page that asks who to sign in
// as, a page that needs a user, sign out, and the window closing. CDC.Api is replaced, so the users' rows and roles are whatever the test sets.
public class LoadTestingIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private const string Banner = "Load testing is on";

    private readonly CdcWebTestFactory _factory;

    public LoadTestingIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    // CDC.Api "holds" the two users, each found by its own email address and only for the kind of sign-in it could really make.
    private static FakeApiClient ApiWithTheTwoUsers() => new()
    {
        LookUpUser = (kind, emailAddress) => (kind, emailAddress) switch
        {
            (UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress) => LoadTestUsers.Found(LoadTestUsers.Internal()),
            (UserLookupKind.External, LoadTestUsers.ExternalEmailAddress) => LoadTestUsers.Found(LoadTestUsers.External()),
            (_, LoadTestUsers.InternalEmailAddress or LoadTestUsers.ExternalEmailAddress) => LoadTestUsers.Refused(UserLookupOutcome.NotPermitted),
            _ => LoadTestUsers.Refused(UserLookupOutcome.NotFound)
        }
    };

    private WebApplicationFactory<Program> Factory(
        FakeApiClient api,
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

            if (deployment is not null)
            {
                builder.UseSetting("Deployment:Environment", deployment);
            }

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(appClock);
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => options.TimeProvider = appClock);
                services.PostConfigure<CookieAuthenticationOptions>(EntraAuthenticationDefaults.CookieAuthenticationScheme, options => options.TimeProvider = appClock);
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(api);
            });
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    // Does what a tester or a load script does: asks for the page for the journey, reads the antiforgery token off its form, and posts the email address.
    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string kind, string emailAddress, string? returnUrl = null)
    {
        var page = await client.GetStringAsync($"/Account/LoadTestLogin?kind={kind}" + (returnUrl is null ? string.Empty : $"&returnUrl={Uri.EscapeDataString(returnUrl)}"));
        var token = Regex.Match(page, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(token.Success, "The Load test login page must carry an antiforgery token.");

        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token.Groups[1].Value),
            new("Kind", kind),
            new("EmailAddress", emailAddress)
        };
        if (returnUrl is not null)
        {
            form.Add(new("ReturnUrl", returnUrl));
        }

        return await client.PostAsync("/Account/LoadTestLogin", new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task LoginInternal_SendsTheUserToTheLoadTestLoginPageNotToEntra()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var login = await client.GetAsync("/Account/LoginInternal");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Account/LoadTestLogin?kind=internal", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Login_SendsTheUserToTheLoadTestLoginPageNotToCidm()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var login = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Account/LoadTestLogin?kind=external", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task TheLoadTestLoginPage_HasTheLandingPagesHeaderAndFooter_AndLoginAsBesideItsTextboxAboveTheLoginButton()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var landing = await client.GetStringAsync("/");
        var response = await client.GetAsync("/Account/LoadTestLogin?kind=internal");
        var page = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Load test login - D2R2</title>", page, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Load test login</h1>", page, StringComparison.Ordinal);
        Assert.Contains(Banner, page, StringComparison.Ordinal);

        // The landing page hides the account bar and the footer navigation; this page must show exactly the same header and footer.
        foreach (var shown in new[] { "govuk-header__product-name", "govuk-footer__meta" })
        {
            Assert.Contains(shown, landing, StringComparison.Ordinal);
            Assert.Contains(shown, page, StringComparison.Ordinal);
        }

        foreach (var hidden in new[] { "app-menu-toggle", "govuk-back-to-top-link" })
        {
            Assert.DoesNotContain(hidden, landing, StringComparison.Ordinal);
            Assert.DoesNotContain(hidden, page, StringComparison.Ordinal);
        }

        Assert.Matches(
            new Regex("""for="EmailAddress">Login as</label>\s*<input[^>]*id="EmailAddress"[^>]*type="email"[\s\S]*?>Login</button>""", RegexOptions.None, TimeSpan.FromSeconds(5)),
            page);
    }

    [Fact]
    public async Task LoginInternal_SignsInTheUserWhoseEmailAddressWasEntered()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var login = await PostLoginAsync(client, "internal", LoadTestUsers.InternalEmailAddress);
        var home = await client.GetStringAsync("/Landing/Internal");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Landing/Internal", login.Headers.Location!.OriginalString);
        Assert.Contains("Load Test Internal", home, StringComparison.Ordinal);
        Assert.Contains(Banner, home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginInternal_GivesTheRoleThePermissionFlagsOfTheEnteredUsersRecordSay()
    {
        var api = new FakeApiClient
        {
            LookUpUser = (_, emailAddress) => LoadTestUsers.Found(LoadTestUsers.Internal(isProfileEditor: emailAddress.StartsWith("editor", StringComparison.Ordinal)))
        };
        using var factory = Factory(api);
        var editor = Client(factory);
        var reader = Client(factory);
        await PostLoginAsync(editor, "internal", "editor@example.test");
        await PostLoginAsync(reader, "internal", "reader@example.test");

        Assert.Contains("Create profile", await editor.GetStringAsync("/Landing/Internal"), StringComparison.Ordinal);
        Assert.DoesNotContain("Create profile", await reader.GetStringAsync("/Landing/Internal"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_SignsInTheUserWhoseEmailAddressWasEntered()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var login = await PostLoginAsync(client, "external", LoadTestUsers.ExternalEmailAddress);
        var home = await client.GetStringAsync("/Landing/External");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Landing/External", login.Headers.Location!.OriginalString);
        Assert.Contains("Load Test External", home, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUserSignedInOneWay_CannotSignInTheOtherWayInTheSameBrowser()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);
        await PostLoginAsync(client, "internal", LoadTestUsers.InternalEmailAddress);

        var external = await client.GetAsync("/Account/Login");
        var page = await client.GetAsync("/Account/LoadTestLogin?kind=external");

        Assert.Equal("/Account/AlreadySignedIn", external.Headers.Location!.OriginalString);
        Assert.Equal("/Account/AlreadySignedIn", page.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task APageThatNeedsAnInternalUser_SendsAnAnonymousRequestToTheLoadTestLoginAndBackAfterTheEmailAddressIsEntered()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var challenge = await client.GetAsync("/Landing/Internal");
        var login = await client.GetAsync(challenge.Headers.Location!.PathAndQuery);
        var loginPage = await client.GetAsync(login.Headers.Location!.OriginalString);
        var signedIn = await PostLoginAsync(client, "internal", LoadTestUsers.InternalEmailAddress, returnUrl: "/Landing/Internal");
        var page = await client.GetAsync(signedIn.Headers.Location!.OriginalString);

        Assert.Equal("/Account/LoginInternal", challenge.Headers.Location.AbsolutePath);
        Assert.StartsWith("/Account/LoadTestLogin?kind=internal", login.Headers.Location.OriginalString, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2FLanding%2FInternal", login.Headers.Location.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        Assert.Equal("/Landing/Internal", signedIn.Headers.Location.OriginalString);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task APageThatNeedsAnExternalUser_SendsAnAnonymousRequestToTheLoadTestLoginAndBackAfterTheEmailAddressIsEntered()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var challenge = await client.GetAsync("/Landing/External");
        var login = await client.GetAsync(challenge.Headers.Location!.PathAndQuery);
        var loginPage = await client.GetAsync(login.Headers.Location!.OriginalString);
        var signedIn = await PostLoginAsync(client, "external", LoadTestUsers.ExternalEmailAddress, returnUrl: "/Landing/External");
        var page = await client.GetAsync(signedIn.Headers.Location!.OriginalString);

        Assert.Equal("/Account/Login", challenge.Headers.Location.AbsolutePath);
        Assert.StartsWith("/Account/LoadTestLogin?kind=external", login.Headers.Location.OriginalString, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2FLanding%2FExternal", login.Headers.Location.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        Assert.Equal("/Landing/External", signedIn.Headers.Location.OriginalString);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task WithNoEmailAddress_ThePageAsksForOneAndNoSessionIsCreated()
    {
        var api = ApiWithTheTwoUsers();
        using var factory = Factory(api);
        var client = Client(factory);

        var response = await PostLoginAsync(client, "internal", "   ");
        var page = await response.Content.ReadAsStringAsync();
        var home = await client.GetAsync("/Landing/Internal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Error: Load test login - D2R2</title>", page, StringComparison.Ordinal);
        Assert.Contains("There is a problem", page, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#EmailAddress\">Enter an email address</a>", page, StringComparison.Ordinal);
        Assert.Contains("govuk-form-group govuk-form-group--error", page, StringComparison.Ordinal);
        Assert.Contains("govuk-input--error", page, StringComparison.Ordinal);
        Assert.Empty(api.UserLookups);
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
    }

    [Theory]
    [InlineData("internal", LoadTestUsers.ExternalEmailAddress, "That user cannot sign in as an internal user")]
    [InlineData("external", LoadTestUsers.InternalEmailAddress, "That user cannot sign in as an external user")]
    [InlineData("internal", "nobody@example.test", "No user has that email address")]
    public async Task WhenTheEmailAddressCannotBeUsed_NoSessionIsCreatedAndThePageSaysWhy(string kind, string emailAddress, string expectedMessage)
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var response = await PostLoginAsync(client, kind, emailAddress);
        var page = await response.Content.ReadAsStringAsync();
        var home = await client.GetAsync("/Landing/Internal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedMessage, page, StringComparison.Ordinal);
        Assert.Contains($"value=\"{emailAddress}\"", page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
    }

    [Fact]
    public async Task WhenCdcApiCannotBeReached_ThePageSaysSoAndNoSessionIsCreated()
    {
        var api = new FakeApiClient { LookUpUser = (_, _) => throw new HttpRequestException("connection refused") };
        using var factory = Factory(api);
        var client = Client(factory);

        var response = await PostLoginAsync(client, "external", LoadTestUsers.ExternalEmailAddress);
        var home = await client.GetAsync("/Landing/External");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The user could not be looked up", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
    }

    [Fact]
    public async Task APostWithoutTheAntiforgeryToken_IsRefusedAndNobodyIsLookedUp()
    {
        var api = ApiWithTheTwoUsers();
        using var factory = Factory(api);
        var client = Client(factory);
        await client.GetAsync("/Account/LoadTestLogin?kind=internal");

        var response = await client.PostAsync(
            "/Account/LoadTestLogin",
            new FormUrlEncodedContent([new("Kind", "internal"), new("EmailAddress", LoadTestUsers.InternalEmailAddress)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.UserLookups);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    public async Task APostedReturnUrlThatIsNotLocal_IsIgnored(string returnUrl)
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);

        var login = await PostLoginAsync(client, "internal", LoadTestUsers.InternalEmailAddress, returnUrl);

        Assert.Equal("/Landing/Internal", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LogoutInternal_EndsTheSessionWithoutGoingToEntra()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);
        await PostLoginAsync(client, "internal", LoadTestUsers.InternalEmailAddress);

        var logout = await client.GetAsync("/Account/LogoutInternal");
        var home = await client.GetAsync("/Landing/Internal");

        Assert.Equal("/Account/SignedOut", logout.Headers.Location!.OriginalString);
        Assert.Equal("/Account/LoginInternal", home.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Logout_EndsTheSessionWithoutGoingToCidm()
    {
        using var factory = Factory(ApiWithTheTwoUsers());
        var client = Client(factory);
        await PostLoginAsync(client, "external", LoadTestUsers.ExternalEmailAddress);

        var logout = await client.GetAsync("/Account/Logout");
        var home = await client.GetAsync("/Landing/External");

        Assert.Equal("/Account/SignedOut", logout.Headers.Location!.OriginalString);
        Assert.Equal("/Account/Login", home.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task ThePublicLandingPage_WarnsThatLoadTestingIsOn()
    {
        using var factory = Factory(ApiWithTheTwoUsers());

        var page = await Client(factory).GetStringAsync("/");

        Assert.Contains(Banner, page, StringComparison.Ordinal);
        Assert.Contains("It switches itself off at", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterTheWindowEnds_ASessionKeptAliveByActivityIsEndedAndSignInGoesToTheIdentityProviderAgain()
    {
        var clock = new ControllableClock(DateTimeOffset.UtcNow);
        using var factory = Factory(ApiWithTheTwoUsers(), clock, window: TimeSpan.FromMinutes(10));
        var client = Client(factory);
        await PostLoginAsync(client, "internal", LoadTestUsers.InternalEmailAddress);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Landing/Internal")).StatusCode);

        // Past the window but inside the 20-minute idle timeout, so only the end of the window can have stopped the session.
        clock.Advance(TimeSpan.FromMinutes(15));
        var ended = await client.GetAsync("/Landing/Internal");
        var challenge = await Client(factory).GetAsync("/Landing/Internal");
        var login = await Client(factory).GetAsync("/Account/LoginInternal");
        var loginPage = await Client(factory).GetAsync("/Account/LoadTestLogin?kind=internal");

        Assert.Equal("/", ended.Headers.Location!.OriginalString);
        Assert.Contains(ended.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith(".AspNetCore.Cookies.Entra=;", StringComparison.Ordinal));
        Assert.Equal("/Account/LoginInternal", challenge.Headers.Location!.AbsolutePath);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, login.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, loginPage.StatusCode);
    }

    [Fact]
    public async Task AfterTheWindowEnds_ExternalSignInGoesToCidmAgain()
    {
        var clock = new ControllableClock(DateTimeOffset.UtcNow);
        using var factory = Factory(ApiWithTheTwoUsers(), clock, window: TimeSpan.FromMinutes(10));
        clock.Advance(TimeSpan.FromMinutes(15));

        var login = await Client(factory).GetAsync("/Account/Login");

        Assert.StartsWith(CdcWebTestFactory.FakeAuthorizationEndpoint, login.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenOff_ARequestForAPageThatNeedsAUserStillGoesStraightToTheIdentityProvider()
    {
        // Switched off explicitly: a developer's own appsettings.Development.json may switch it on.
        await using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("LoadTesting:Enabled", "false"));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var response = await client.GetAsync("/Landing/Internal");
        var landing = await client.GetStringAsync("/");
        var loginPage = await client.GetAsync("/Account/LoadTestLogin?kind=internal");

        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Banner, landing, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, loginPage.StatusCode);
    }

    [Fact]
    public void InProduction_TheAppRefusesToStart()
    {
        using var factory = Factory(new FakeApiClient(), deployment: "PROD");

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("'PROD'", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WithAWindowOverAWeek_TheAppRefusesToStart()
    {
        using var factory = Factory(new FakeApiClient(), window: TimeSpan.FromDays(8));

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("7 days", failure.ToString(), StringComparison.Ordinal);
    }
}
