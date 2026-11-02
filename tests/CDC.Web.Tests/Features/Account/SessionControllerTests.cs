using System.Reflection;
using System.Security.Claims;
using CDC.Common.Auth;
using CDC.Web.Features.Account;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Features.Account;

public class SessionControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static SessionController CreateController(
        out FakeAuthenticationService authService,
        ClaimsPrincipal? user = null,
        TimeSpan clockOffset = default,
        bool validAntiforgeryToken = true)
    {
        authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);

        return new SessionController(
            Microsoft.Extensions.Options.Options.Create(new SessionTimeoutOptions()),
            new FakeAntiforgery { RequestIsValid = validAntiforgeryToken },
            new FixedTimeProvider(Now + clockOffset))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services.BuildServiceProvider(),
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            },
            Url = new FakeUrlHelper()
        };
    }

    private static AuthenticateResult SignedInUntil(DateTimeOffset? expiresUtc) =>
        AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            new AuthenticationProperties { ExpiresUtc = expiresUtc },
            "test"));

    private static ClaimsPrincipal SignedInUser() => new(new ClaimsIdentity("test"));

    [Fact]
    public async Task Status_WhenSignedOut_ReportsTheSessionHasEnded()
    {
        var controller = CreateController(out _);

        var result = await controller.Status();

        Assert.Equal(SessionStatus.Ended, Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public async Task Status_WhenSignedIn_ReportsTheTimeLeftWithTheTokenToExtendItWith()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultToReturn = SignedInUntil(Now.AddMinutes(10));

        var result = await controller.Status();

        Assert.Equal(
            new SessionStatus(true, 600, FakeAntiforgery.RequestToken, FakeAntiforgery.HeaderName),
            Assert.IsType<JsonResult>(result).Value);
    }

    // A ticket's expiry is held to the second, but the clock is not.
    [Fact]
    public async Task Status_RoundsAPartSecondUp_SoASessionWithAnyTimeLeftNeverReportsNone()
    {
        var controller = CreateController(out var authService, clockOffset: TimeSpan.FromMilliseconds(600));
        authService.AuthenticateResultToReturn = SignedInUntil(Now.AddSeconds(1));

        var result = await controller.Status();

        Assert.Equal(1, Assert.IsType<SessionStatus>(Assert.IsType<JsonResult>(result).Value).RemainingSeconds);
    }

    [Fact]
    public async Task Status_WhenTheSessionHasNoExpiry_ReportsAFullTimeout()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultToReturn = SignedInUntil(null);

        var result = await controller.Status();

        Assert.Equal(20 * 60, Assert.IsType<SessionStatus>(Assert.IsType<JsonResult>(result).Value).RemainingSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task Status_WhenTheSessionIsAtOrPastItsExpiry_ReportsTheSessionHasEnded(int secondsFromNow)
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultToReturn = SignedInUntil(Now.AddSeconds(secondsFromNow));

        var result = await controller.Status();

        Assert.Equal(SessionStatus.Ended, Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public async Task Extend_WhenSignedIn_ReportsTheTimeTheSessionNowHas()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultToReturn = SignedInUntil(Now.AddMinutes(20));

        var result = await controller.Extend();

        Assert.Equal(new SessionStatus(true, 20 * 60), Assert.IsType<JsonResult>(result).Value);
    }

    [Fact]
    public async Task Extend_WhenTheSessionHasEnded_IsUnauthorisedRatherThanRevivingIt()
    {
        var controller = CreateController(out _);

        var result = await controller.Extend();

        var unauthorised = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(SessionStatus.Ended, unauthorised.Value);
    }

    // Extending must not be forgeable from another site, whether or not the session is still alive.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Extend_WithoutAValidAntiforgeryToken_IsRefusedAndTheSessionIsNotReportedOn(bool signedIn)
    {
        var controller = CreateController(out var authService, validAntiforgeryToken: false);
        if (signedIn)
        {
            authService.AuthenticateResultToReturn = SignedInUntil(Now.AddMinutes(20));
        }

        var result = await controller.Extend();

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public void SignInAgain_WhenSignedOut_ChallengesTheUsersIdentityProviderAndReturnsThemToThePageTheyWereOn()
    {
        var controller = CreateController(out _);

        var result = controller.SignInAgain("/SurveillanceProfiles/Search?q=anthrax");

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Empty(challenge.AuthenticationSchemes);
        Assert.Equal("/SurveillanceProfiles/Search?q=anthrax", challenge.Properties!.RedirectUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://elsewhere.example/phish")]
    [InlineData("//elsewhere.example/phish")]
    [InlineData("/\\elsewhere.example/phish")]
    public void SignInAgain_WithNoPageOnThisSiteToReturnTo_ReturnsTheUserToTheirHomePageInstead(string? returnUrl)
    {
        var controller = CreateController(out _);

        var result = controller.SignInAgain(returnUrl);

        Assert.Equal("/Landing/Internal", Assert.IsType<ChallengeResult>(result).Properties!.RedirectUri);
    }

    [Fact]
    public void SignInAgain_WhenTheSessionWasKeptAliveMeanwhile_JustReturnsTheUserToThePage()
    {
        var controller = CreateController(out _, SignedInUser());

        var result = controller.SignInAgain("/SurveillanceProfiles/Search");

        Assert.Equal("/SurveillanceProfiles/Search", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public void SignInAgain_WhenTheSessionWasKeptAliveMeanwhile_DoesNotRedirectToAnotherSite()
    {
        var controller = CreateController(out _, SignedInUser());

        var result = controller.SignInAgain("https://elsewhere.example/phish");

        Assert.Equal("/Landing/Internal", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    // The status check must never be able to keep an idle session alive.
    [Fact]
    public void OnlyTheStatusCheckIsPassive()
    {
        var passive = typeof(SessionController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<PassiveSessionRequestAttribute>() is not null)
            .Select(method => method.Name);

        Assert.Equal([nameof(SessionController.Status)], passive);
    }

    [Fact]
    public void Extend_AcceptsOnlyAPost()
    {
        var extend = typeof(SessionController).GetMethod(nameof(SessionController.Extend))!;

        Assert.NotNull(extend.GetCustomAttribute<HttpPostAttribute>());
    }

    // A script told to sign in again must be answered, not redirected to the identity provider where it cannot follow, and the answers
    // must never be cached by the browser or a proxy.
    [Fact]
    public void TheControllerAllowsAnonymousRequestsAndForbidsCaching()
    {
        Assert.NotNull(typeof(SessionController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.True(typeof(SessionController).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
