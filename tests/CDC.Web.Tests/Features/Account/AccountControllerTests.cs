using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Models;
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
        var controller = new AccountController(new FakeAntiforgery())
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
        var controller = new AccountController(new FakeAntiforgery())
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
        var controller = new AccountController(new FakeAntiforgery());

        var result = controller.SignedOut();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void NotPermitted_ReturnsView()
    {
        var controller = new AccountController(new FakeAntiforgery());

        var result = controller.NotPermitted();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void AccessDenied_ReturnsViewWithForbiddenStatus()
    {
        var controller = new AccountController(new FakeAntiforgery())
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
        var controller = new AccountController(new FakeAntiforgery())
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
        var controller = new AccountController(new FakeAntiforgery())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper()
        };

        var result = await controller.LogoutInternal();

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal("captured-id-token", signOut.Properties!.GetTokenValue("id_token"));
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActive_SendsTheUserToTheLoadTestLoginPageForTheExternalJourney()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _);

        var result = await controller.Login();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("LoadTestLogin", redirect.ActionName);
        Assert.Equal("external", redirect.RouteValues!["kind"]);
        Assert.Null(redirect.RouteValues["returnUrl"]);
        Assert.Empty(loadTest.SignIns);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActive_CarriesTheReturnUrlToTheLoadTestLoginPage()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.Login("/SurveillanceProfiles/Search");

        Assert.Equal("/SurveillanceProfiles/Search", Assert.IsType<RedirectToActionResult>(result).RouteValues!["returnUrl"]);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActiveButAnInternalSessionExists_StillStopsTheUser()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out var authService);
        authService.AuthenticateResultsByScheme[EntraAuthenticationDefaults.CookieAuthenticationScheme] = SignedIn(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var result = await controller.Login();

        Assert.Equal("AlreadySignedIn", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsActiveButACidmSessionExists_GoesStraightToTheExternalHome()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.Login();

        Assert.Equal("External", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task Login_WhenLoadTestingIsRegisteredButNotActive_StillChallengesCidm()
    {
        var controller = CreateController(new FakeLoadTestSignIn(isActive: false), out _);

        var result = await controller.Login();

        Assert.Equal([CidmAuthenticationDefaults.AuthenticationScheme], Assert.IsType<ChallengeResult>(result).AuthenticationSchemes);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsActive_SendsTheUserToTheLoadTestLoginPageForTheInternalJourney()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoginInternal();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("LoadTestLogin", redirect.ActionName);
        Assert.Equal("internal", redirect.RouteValues!["kind"]);
        Assert.Null(redirect.RouteValues["returnUrl"]);
        Assert.Empty(loadTest.SignIns);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsActive_CarriesTheReturnUrlToTheLoadTestLoginPage()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.LoginInternal("/CrossProfileAdmin/ReferenceData");

        Assert.Equal("/CrossProfileAdmin/ReferenceData", Assert.IsType<RedirectToActionResult>(result).RouteValues!["returnUrl"]);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsActiveButAnExternalSessionExists_StillStopsTheUser()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out var authService);
        authService.AuthenticateResultsByScheme[CookieAuthenticationDefaults.AuthenticationScheme] = SignedIn(CookieAuthenticationDefaults.AuthenticationScheme);

        var result = await controller.LoginInternal();

        Assert.Equal("AlreadySignedIn", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task LoginInternal_WhenLoadTestingIsRegisteredButNotActive_StillChallengesEntra()
    {
        var controller = CreateController(new FakeLoadTestSignIn(isActive: false), out _);

        var result = await controller.LoginInternal();

        Assert.Equal([EntraAuthenticationDefaults.AuthenticationScheme], Assert.IsType<ChallengeResult>(result).AuthenticationSchemes);
    }

    [Theory]
    [InlineData("internal", "internal")]
    [InlineData("INTERNAL", "internal")]
    [InlineData("external", "external")]
    [InlineData("External", "external")]
    public async Task LoadTestLogin_ShowsThePageForTheJourneyChosen(string kind, string expectedKind)
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.LoadTestLogin(kind, "/SurveillanceProfiles/Search");

        var model = Assert.IsType<LoadTestLoginViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(expectedKind, model.Kind);
        Assert.Equal("/SurveillanceProfiles/Search", model.ReturnUrl);
        Assert.Null(model.EmailAddress);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public async Task LoadTestLogin_DropsAReturnUrlThatIsNotLocal(string? returnUrl)
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.LoadTestLogin("internal", returnUrl);

        Assert.Null(Assert.IsType<LoadTestLoginViewModel>(Assert.IsType<ViewResult>(result).Model).ReturnUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("internal ")]
    public async Task LoadTestLogin_ForAJourneyThatDoesNotExist_IsNotFound(string? kind)
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        Assert.IsType<NotFoundResult>(await controller.LoadTestLogin(kind));
        Assert.IsType<NotFoundResult>(await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = kind ?? string.Empty, EmailAddress = "someone@example.test" }));
    }

    [Fact]
    public async Task LoadTestLogin_WithoutAValidAntiforgeryToken_IsABadRequestAndSignsNobodyIn()
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _, new FakeAntiforgery { RequestIsValid = false });

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "internal", EmailAddress = "someone@example.test" });

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(loadTest.SignIns);
    }

    [Fact]
    public async Task LoadTestLogin_WhenLoadTestingIsNotActive_IsNotFound()
    {
        var notActive = CreateController(new FakeLoadTestSignIn(isActive: false), out _);
        var notRegistered = CreateController(out _);

        Assert.IsType<NotFoundResult>(await notActive.LoadTestLogin("internal"));
        Assert.IsType<NotFoundResult>(await notRegistered.LoadTestLogin("external"));
        Assert.IsType<NotFoundResult>(await notActive.LoadTestLogin(new LoadTestLoginViewModel { Kind = "internal", EmailAddress = "someone@example.test" }));
        Assert.IsType<NotFoundResult>(await notRegistered.LoadTestLogin(new LoadTestLoginViewModel { Kind = "external", EmailAddress = "someone@example.test" }));
    }

    [Theory]
    [InlineData("internal", EntraAuthenticationDefaults.CookieAuthenticationScheme, "Internal")]
    [InlineData("external", CookieAuthenticationDefaults.AuthenticationScheme, "External")]
    public async Task LoadTestLogin_WhenTheUserAlreadyHasASessionOfThatKind_GoesStraightToTheirHome(string kind, string cookieScheme, string home)
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out var authService);
        authService.AuthenticateResultsByScheme[cookieScheme] = SignedIn(cookieScheme);

        var page = await controller.LoadTestLogin(kind);
        var post = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = kind, EmailAddress = "someone@example.test" });

        Assert.Equal(home, Assert.IsType<RedirectToActionResult>(page).ActionName);
        Assert.Equal(home, Assert.IsType<RedirectToActionResult>(post).ActionName);
        Assert.Empty(loadTest.SignIns);
    }

    [Theory]
    [InlineData("internal", CookieAuthenticationDefaults.AuthenticationScheme)]
    [InlineData("external", EntraAuthenticationDefaults.CookieAuthenticationScheme)]
    public async Task LoadTestLogin_WhenTheUserHasASessionOfTheOtherKind_StopsThemAsTheRealLoginDoes(string kind, string otherCookieScheme)
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out var authService);
        authService.AuthenticateResultsByScheme[otherCookieScheme] = SignedIn(otherCookieScheme);

        var page = await controller.LoadTestLogin(kind);
        var post = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = kind, EmailAddress = "someone@example.test" });

        Assert.Equal("AlreadySignedIn", Assert.IsType<RedirectToActionResult>(page).ActionName);
        Assert.Equal("AlreadySignedIn", Assert.IsType<RedirectToActionResult>(post).ActionName);
        Assert.Empty(loadTest.SignIns);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadTestLogin_WithNoEmailAddress_AsksForOneAndSignsNobodyIn(string? emailAddress)
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "internal", EmailAddress = emailAddress });

        var model = Assert.IsType<LoadTestLoginViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("internal", model.Kind);
        Assert.Equal("Enter an email address", Assert.Single(controller.ModelState[nameof(LoadTestLoginViewModel.EmailAddress)]!.Errors).ErrorMessage);
        Assert.Empty(loadTest.SignIns);
    }

    [Theory]
    [InlineData("internal", UserLookupKind.Internal, "/Landing/Internal")]
    [InlineData("external", UserLookupKind.External, "/Landing/External")]
    public async Task LoadTestLogin_SignsInTheUserByTheTrimmedEmailAddressAndGoesToTheirHome(string kind, UserLookupKind expectedKind, string expectedHome)
    {
        var loadTest = new FakeLoadTestSignIn();
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = kind, EmailAddress = " someone@example.test " });

        Assert.Equal(expectedHome, Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal((expectedKind, "someone@example.test"), Assert.Single(loadTest.SignIns));
    }

    [Fact]
    public async Task LoadTestLogin_ReturnsToALocalReturnUrl()
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "internal", EmailAddress = "someone@example.test", ReturnUrl = "/SurveillanceProfiles/Search" });

        Assert.Equal("/SurveillanceProfiles/Search", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public async Task LoadTestLogin_IgnoresAReturnUrlThatIsNotLocal(string? returnUrl)
    {
        var controller = CreateController(new FakeLoadTestSignIn(), out _);

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "external", EmailAddress = "someone@example.test", ReturnUrl = returnUrl });

        Assert.Equal("/Landing/External", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Theory]
    [InlineData(LoadTestSignInOutcome.NotFound, "No user has that email address")]
    [InlineData(LoadTestSignInOutcome.NotPermitted, "That user cannot sign in as an internal user")]
    [InlineData(LoadTestSignInOutcome.Ambiguous, "More than one user has that email address, so none can be chosen")]
    [InlineData(LoadTestSignInOutcome.InvalidEmailAddress, "Enter an email address in the correct format, like name@example.com")]
    [InlineData(LoadTestSignInOutcome.Unavailable, "The user could not be looked up. Try again later")]
    public async Task LoadTestLogin_WhenTheUserCannotBeSignedIn_ShowsThePageAgainWithWhy(LoadTestSignInOutcome outcome, string expectedMessage)
    {
        var loadTest = new FakeLoadTestSignIn { Outcome = outcome };
        var controller = CreateController(loadTest, out _);

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "Internal", EmailAddress = "someone@example.test", ReturnUrl = "/SurveillanceProfiles/Search" });

        var model = Assert.IsType<LoadTestLoginViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("internal", model.Kind);
        Assert.Equal("someone@example.test", model.EmailAddress);
        Assert.Equal("/SurveillanceProfiles/Search", model.ReturnUrl);
        Assert.Equal(expectedMessage, Assert.Single(controller.ModelState[nameof(LoadTestLoginViewModel.EmailAddress)]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task LoadTestLogin_WhenAnExternalUserCannotBeSignedIn_NamesTheExternalJourneyInTheMessage()
    {
        var controller = CreateController(new FakeLoadTestSignIn { Outcome = LoadTestSignInOutcome.NotPermitted }, out _);

        await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "external", EmailAddress = "someone@example.test" });

        Assert.Equal("That user cannot sign in as an external user", Assert.Single(controller.ModelState[nameof(LoadTestLoginViewModel.EmailAddress)]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task LoadTestLogin_AfterAFailure_ShowsTheCheckedJourneyAndReturnUrlNotWhatWasPosted()
    {
        var controller = CreateController(new FakeLoadTestSignIn { Outcome = LoadTestSignInOutcome.NotFound }, out _);
        controller.ModelState.SetModelValue(nameof(LoadTestLoginViewModel.ReturnUrl), "https://evil.example/x", "https://evil.example/x");

        var result = await controller.LoadTestLogin(new LoadTestLoginViewModel { Kind = "INTERNAL", EmailAddress = "someone@example.test", ReturnUrl = "https://evil.example/x" });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(Assert.IsType<LoadTestLoginViewModel>(view.Model).ReturnUrl);
        Assert.False(view.ViewData.ModelState.ContainsKey(nameof(LoadTestLoginViewModel.ReturnUrl)));
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

    private static AccountController CreateController(ILoadTestSignIn? loadTestSignIn, out FakeAuthenticationService authService, FakeAntiforgery? antiforgery = null)
    {
        var httpContext = CreateHttpContextWithFakeAuth(out authService);

        return new AccountController(antiforgery ?? new FakeAntiforgery(), loadTestSignIn)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            Url = new FakeUrlHelper(),
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private sealed class FakeLoadTestSignIn(bool isActive = true) : ILoadTestSignIn
    {
        public bool IsActive { get; } = isActive;

        public LoadTestSignInOutcome Outcome { get; set; } = LoadTestSignInOutcome.SignedIn;

        public List<(UserLookupKind Kind, string EmailAddress)> SignIns { get; } = [];

        public Task<LoadTestSignInOutcome> SignInAsync(HttpContext httpContext, UserLookupKind kind, string emailAddress)
        {
            SignIns.Add((kind, emailAddress));
            return Task.FromResult(Outcome);
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
