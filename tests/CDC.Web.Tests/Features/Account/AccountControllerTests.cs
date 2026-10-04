using System.Security.Claims;
using CDC.Auth.Cidm;
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
    public void Login_WhenAnonymous_ChallengesCidmSchemeWithRedirectToLandingExternal()
    {
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            Url = new FakeUrlHelper()
        };

        var result = controller.Login();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
        Assert.Equal("/Landing/External", challenge.Properties!.RedirectUri);
    }

    [Fact]
    public void Login_WhenAlreadyAuthenticated_RedirectsToLandingExternal()
    {
        var identity = new ClaimsIdentity(authenticationType: CookieAuthenticationDefaults.AuthenticationScheme);
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            },
            Url = new FakeUrlHelper()
        };

        var result = controller.Login();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("External", redirect.ActionName);
        Assert.Equal("Landing", redirect.ControllerName);
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

    private static DefaultHttpContext CreateHttpContextWithFakeAuth(out FakeAuthenticationService authService)
    {
        authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }
}
