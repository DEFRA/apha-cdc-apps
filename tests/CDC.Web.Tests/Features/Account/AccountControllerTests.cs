using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;
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

    // Choosing to sign in as an external user while signed in as an internal one goes to CIDM; the internal session is not mistaken for it.
    [Fact]
    public async Task Login_WhenOnlyAnEntraSessionExists_StillChallengesCidm()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[EntraAuthenticationDefaults.CookieAuthenticationScheme] = SignedIn(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var result = await controller.Login();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
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

    // Choosing to sign in as an internal user while signed in as an external one goes to Entra ID; the external session is not mistaken for it.
    [Fact]
    public async Task LoginInternal_WhenOnlyACidmSessionExists_StillChallengesEntra()
    {
        var controller = CreateController(out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.LoginInternal();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
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

    private static AccountController CreateController(out FakeAuthenticationService authService) => new()
    {
        ControllerContext = new ControllerContext { HttpContext = CreateHttpContextWithFakeAuth(out authService) },
        Url = new FakeUrlHelper()
    };

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
