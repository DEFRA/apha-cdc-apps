using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Features.Account;

public class AccountControllerTests
{
    [Fact]
    public async Task Login_WhenAnonymous_ChallengesCidmSchemeWithRedirectToLandingExternal()
    {
        var controller = CreateController(out _);

        var result = await controller.Login();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
        Assert.Equal("/Landing/External", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public async Task Login_WhenACidmSessionExists_RedirectsToLandingExternal()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.Login();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("External", redirect.ActionName);
        Assert.Equal("Landing", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_WhenSignedInInternally_RedirectsToAlreadySignedInInsteadOfChallenging()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[EntraAuthenticationDefaults.CookieAuthenticationScheme] = SignedIn(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var result = await controller.Login();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("AlreadySignedIn", redirect.ActionName);
    }

    [Fact]
    public async Task Logout_SignsOutCookieSchemeWithNoRedirectThenReturnsCidmSignOutResult()
    {
        var httpContext = CreateHttpContextWithFakeAuth(out var authService);
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.Logout();

        // Signed out separately, not via a single SignOut(cookie, cidm) result sharing one
        // AuthenticationProperties - CookieAuthenticationHandler also honours RedirectUri and would
        // otherwise hijack the response before the cidm scheme's sign-out ever runs.
        var cookieSignOut = Assert.Single(authService.SignOutCalls, call => call.Scheme == CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Null(cookieSignOut.Properties?.RedirectUri);

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], signOut.AuthenticationSchemes);
        Assert.Equal("/Account/SignedOut", signOut.Properties!.RedirectUri);
    }

    [Fact]
    public async Task Logout_WhenCookieCarriesAnIdToken_ForwardsItAsIdTokenHintToCidmSignOut()
    {
        var httpContext = CreateHttpContextWithFakeAuth(out var authService);
        var cookieProperties = new AuthenticationProperties();
        cookieProperties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = "captured-id-token" }]);
        var identity = new ClaimsIdentity(authenticationType: CookieAuthenticationDefaults.AuthenticationScheme);
        authService.AuthenticateResultToReturn = AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), cookieProperties, CookieAuthenticationDefaults.AuthenticationScheme));
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.Logout();

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal("captured-id-token", signOut.Properties!.GetTokenValue("id_token"));
    }

    [Fact]
    public void SignedOut_ReturnsView()
    {
        var controller = new AccountController();

        var result = controller.SignedOut();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void NotPermitted_ReturnsView()
    {
        var controller = new AccountController();

        var result = controller.NotPermitted();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void AccessDenied_ReturnsViewWithForbiddenStatus()
    {
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.AccessDenied();

        Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task LoginInternal_WhenAnonymous_ChallengesEntraSchemeWithRedirectToLandingInternal()
    {
        var controller = CreateController(out _);

        var result = await controller.LoginInternal();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
        Assert.Equal("/Landing/Internal", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public async Task LoginInternal_WhenAnEntraSessionExists_RedirectsToLandingInternal()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[EntraAuthenticationDefaults.CookieAuthenticationScheme] = SignedIn(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var result = await controller.LoginInternal();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Internal", redirect.ActionName);
        Assert.Equal("Landing", redirect.ControllerName);
    }

    [Fact]
    public async Task LoginInternal_WhenSignedInExternally_RedirectsToAlreadySignedInInsteadOfChallenging()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.LoginInternal();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("AlreadySignedIn", redirect.ActionName);
    }

    [Fact]
    public async Task AlreadySignedIn_WhenSignedInInternally_ShowsInternalSessionWithInternalHome()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[EntraAuthenticationDefaults.CookieAuthenticationScheme] = SignedIn(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var result = await controller.AlreadySignedIn();

        var model = Assert.IsType<AlreadySignedInViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("internal", model.SessionName);
        Assert.Equal("Internal", model.HomeAction);
    }

    [Fact]
    public async Task AlreadySignedIn_WhenSignedInExternally_ShowsExternalSessionWithExternalHome()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.AlreadySignedIn();

        var model = Assert.IsType<AlreadySignedInViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("external", model.SessionName);
        Assert.Equal("External", model.HomeAction);
    }

    [Fact]
    public async Task AlreadySignedIn_WhenNotSignedIn_RedirectsToLandingIndex()
    {
        var controller = CreateController(out _);

        var result = await controller.AlreadySignedIn();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Landing", redirect.ControllerName);
    }

    [Fact]
    public async Task LogoutInternal_SignsOutEntraCookieSchemeWithNoRedirectThenReturnsEntraSignOutResult()
    {
        var httpContext = CreateHttpContextWithFakeAuth(out var authService);
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.LogoutInternal();

        var cookieSignOut = Assert.Single(authService.SignOutCalls, call => call.Scheme == EntraAuthenticationDefaults.CookieAuthenticationScheme);
        Assert.Null(cookieSignOut.Properties?.RedirectUri);

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], signOut.AuthenticationSchemes);
        Assert.Equal("/Account/SignedOut", signOut.Properties!.RedirectUri);
    }

    [Fact]
    public async Task LogoutInternal_WhenCookieCarriesAnIdToken_ForwardsItAsIdTokenHintToEntraSignOut()
    {
        var httpContext = CreateHttpContextWithFakeAuth(out var authService);
        var cookieProperties = new AuthenticationProperties();
        cookieProperties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = "captured-id-token" }]);
        var identity = new ClaimsIdentity(authenticationType: EntraAuthenticationDefaults.CookieAuthenticationScheme);
        authService.AuthenticateResultToReturn = AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), cookieProperties, EntraAuthenticationDefaults.CookieAuthenticationScheme));
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.LogoutInternal();

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal("captured-id-token", signOut.Properties!.GetTokenValue("id_token"));
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActive_SignsInTheExternalUserInsteadOfChallengingCidm()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _);

        var result = await controller.Login();

        Assert.Equal("/Landing/External", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal(1, loadTest.ExternalSignIns);
        Assert.Equal(0, loadTest.InternalSignIns);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActive_ReturnsToALocalReturnUrl()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.Login("/SurveillanceProfiles/Search");

        Assert.Equal("/SurveillanceProfiles/Search", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Login_WhenLoadTestingIsActive_IgnoresAReturnUrlThatIsNotLocal(string? returnUrl)
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.Login(returnUrl);

        Assert.Equal("/Landing/External", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Fact]
    public async Task Login_WhenLoadTestingRefusesTheUser_RedirectsToWhereItSays()
    {
        var loadTest = new FakeLoadTestSignIn { Result = LoadTestSignInResult.Denied("/Account/NotPermitted") };
        var controller = CreateController(loadTest, out _);

        var result = await controller.Login();

        Assert.Equal("/Account/NotPermitted", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActiveButAnInternalSessionExists_StillStopsTheUser()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out var authService);
        authService.AuthenticateResultsByScheme[EntraAuthenticationDefaults.CookieAuthenticationScheme] = SignedIn(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var result = await controller.Login();

        Assert.Equal("AlreadySignedIn", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(0, loadTest.ExternalSignIns);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActiveButACidmSessionExists_GoesStraightToTheExternalHome()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.Login();

        Assert.Equal("External", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(0, loadTest.ExternalSignIns);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsRegisteredButNotActive_StillChallengesCidm()
    {
        var loadTest = new FakeLoadTestSignIn(isActive: false);
        var controller = CreateController(loadTest, out _);

        var result = await controller.Login();

        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], Assert.IsType<ChallengeResult>(result).AuthenticationSchemes);
        Assert.Equal(0, loadTest.ExternalSignIns);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsActive_SignsInTheInternalUserInsteadOfChallengingEntra()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoginInternal();

        Assert.Equal("/Landing/Internal", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal(1, loadTest.InternalSignIns);
        Assert.Equal(0, loadTest.ExternalSignIns);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsActive_ReturnsToALocalReturnUrlAndIgnoresOthers()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var local = await controller.LoginInternal("/CrossProfileAdmin/ReferenceData");
        var remote = await controller.LoginInternal("https://evil.example/x");

        Assert.Equal("/CrossProfileAdmin/ReferenceData", Assert.IsType<LocalRedirectResult>(local).Url);
        Assert.Equal("/Landing/Internal", Assert.IsType<LocalRedirectResult>(remote).Url);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingRefusesTheUser_RedirectsToWhereItSays()
    {
        var loadTest = new FakeLoadTestSignIn { Result = LoadTestSignInResult.Denied("/Landing/Error") };
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoginInternal();

        Assert.Equal("/Landing/Error", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsActiveButAnExternalSessionExists_StillStopsTheUser()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.LoginInternal();

        Assert.Equal("AlreadySignedIn", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(0, loadTest.InternalSignIns);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsRegisteredButNotActive_StillChallengesEntra()
    {
        var loadTest = new FakeLoadTestSignIn(isActive: false);
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoginInternal();

        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], Assert.IsType<ChallengeResult>(result).AuthenticationSchemes);
        Assert.Equal(0, loadTest.InternalSignIns);
    }

    [Fact]
    public async Task Logout_WhenLoadTestingIsActive_EndsOnlyTheLocalSessionWithoutGoingToCidm()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out var authService);

        var result = await controller.Logout();

        Assert.Equal([CookieAuthenticationDefaults.AuthenticationScheme], authService.SignOutSchemes);
        Assert.Equal("SignedOut", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task LogoutInternal_WhenLoadTestingIsActive_EndsOnlyTheLocalSessionWithoutGoingToEntra()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out var authService);

        var result = await controller.LogoutInternal();

        Assert.Equal([EntraAuthenticationDefaults.CookieAuthenticationScheme], authService.SignOutSchemes);
        Assert.Equal("SignedOut", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    private static AccountController CreateController(out FakeAuthenticationService authService) =>
        CreateController(loadTestSignIn: null, out authService);

    private static AccountController CreateController(ILoadTestSignIn? loadTestSignIn, out FakeAuthenticationService authService)
    {
        var httpContext = CreateHttpContextWithFakeAuth(out authService);

        return new AccountController(loadTestSignIn)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper(),
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private sealed class FakeLoadTestSignIn(bool isActive = true) : ILoadTestSignIn
    {
        public bool IsActive { get; } = isActive;

        public LoadTestSignInResult Result { get; set; } = LoadTestSignInResult.Allowed;

        public int InternalSignIns { get; private set; }

        public int ExternalSignIns { get; private set; }

        public Task<LoadTestSignInResult> SignInInternalAsync(HttpContext httpContext)
        {
            InternalSignIns++;
            return Task.FromResult(Result);
        }

        public Task<LoadTestSignInResult> SignInExternalAsync(HttpContext httpContext)
        {
            ExternalSignIns++;
            return Task.FromResult(Result);
        }
    }

    private sealed class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private static AuthenticateResult SignedIn(string scheme) =>
        AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(authenticationType: scheme)), scheme));

    private static DefaultHttpContext CreateHttpContextWithFakeAuth(out FakeAuthenticationService authService)
    {
        authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }
}
