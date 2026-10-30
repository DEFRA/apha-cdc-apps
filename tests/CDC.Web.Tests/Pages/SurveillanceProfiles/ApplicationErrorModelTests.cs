using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;
using CDC.Web.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ApplicationErrorModelTests
{
    private const string ErrorType = "System.Security.SecurityException";
    private const string Details = "You do not have permission to access the profile data for this profile version section";
    private const string RequestPath = "/SurveillanceProfiles/EditProfileQuestions/7d0e1c3a";

    private static readonly DateTime ErrorTime = new(2026, 10, 28, 19, 41, 37, DateTimeKind.Utc);

    [Fact]
    public void RedirectFrom_RedirectsToTheApplicationErrorPage()
    {
        var (page, _) = CreateRefusingPage();

        var result = ApplicationErrorModel.RedirectFrom(page, ErrorType, Details);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/ApplicationError", redirect.PageName);
    }

    [Fact]
    public void RedirectFrom_Throws_WhenThereIsNoPage() =>
        Assert.Throws<ArgumentNullException>(() => ApplicationErrorModel.RedirectFrom(null!, ErrorType, Details));

    [Fact]
    public void OnGet_ShowsTheTechnicalDetailsThePageThatRefusedTheUserLeft()
    {
        var (page, tempData) = CreateRefusingPage();
        ApplicationErrorModel.RedirectFrom(page, ErrorType, Details);
        var pageModel = CreatePageModel(tempData, ExternalUser());

        pageModel.OnGet();

        Assert.True(pageModel.HasTechnicalDetails);
        Assert.Equal("Jane Doe (jane.doe@example.com)", pageModel.UserDescription);
        Assert.Equal(ErrorType, pageModel.ErrorType);
        Assert.Equal($"https://d2r2.example{RequestPath}?section=3", pageModel.ErrorLocation);
        Assert.Equal("10/28/2026 7:41:37 PM", pageModel.TimeOfError);
        Assert.Equal(Details, pageModel.Details);
    }

    [Fact]
    public void OnGet_ShowsNoTechnicalDetails_WhenNoPageLeftAny()
    {
        var pageModel = CreatePageModel(NewTempData(), ExternalUser());

        pageModel.OnGet();

        Assert.False(pageModel.HasTechnicalDetails);
        Assert.Null(pageModel.UserDescription);
        Assert.Null(pageModel.ErrorType);
        Assert.Null(pageModel.ErrorLocation);
        Assert.Null(pageModel.TimeOfError);
        Assert.Null(pageModel.Details);
    }

    [Fact]
    public void OnGet_NamesAnInternalUserBySigninName_WithTheirUserPrincipalName()
    {
        var (page, tempData) = CreateRefusingPage();
        ApplicationErrorModel.RedirectFrom(page, ErrorType, Details);
        var user = Principal(
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, EntraAuthenticationDefaults.AuthenticationScheme),
            new Claim(ClaimTypes.Name, "Joe Bloggs"),
            new Claim("preferred_username", "joe.bloggs@example.com"));
        var pageModel = CreatePageModel(tempData, user);

        pageModel.OnGet();

        Assert.Equal("Joe Bloggs (joe.bloggs@example.com)", pageModel.UserDescription);
    }

    [Fact]
    public void OnGet_NamesTheUserAlone_WhenTheyHaveNoEmailAddress()
    {
        var (page, tempData) = CreateRefusingPage();
        ApplicationErrorModel.RedirectFrom(page, ErrorType, Details);
        var user = Principal(
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme),
            new Claim(ExternalUserClaimTypes.FullName, "Jane Doe"));
        var pageModel = CreatePageModel(tempData, user);

        pageModel.OnGet();

        Assert.Equal("Jane Doe", pageModel.UserDescription);
    }

    [Fact]
    public void OnGet_NamesTheUserAlone_WhenTheirNameIsTheirEmailAddress()
    {
        var (page, tempData) = CreateRefusingPage();
        ApplicationErrorModel.RedirectFrom(page, ErrorType, Details);
        var user = Principal(
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, EntraAuthenticationDefaults.AuthenticationScheme),
            new Claim(ClaimTypes.Name, "joe.bloggs@example.com"),
            new Claim("preferred_username", "JOE.BLOGGS@example.com"));
        var pageModel = CreatePageModel(tempData, user);

        pageModel.OnGet();

        Assert.Equal("joe.bloggs@example.com", pageModel.UserDescription);
    }

    [Fact]
    public void OnGet_LeadsTheBreadcrumbBackToTheSearchPage()
    {
        var pageModel = CreatePageModel(NewTempData(), ExternalUser());

        pageModel.OnGet();

        Assert.Equal("Browse profile", pageModel.Breadcrumb.PageName);
        Assert.Equal("Search disease profiles", pageModel.Breadcrumb.ParentPageName);
        Assert.Equal("/SurveillanceProfiles/Search", pageModel.Breadcrumb.ParentUrl);
    }

    private static ClaimsPrincipal ExternalUser() =>
        Principal(
            new Claim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme),
            new Claim(ExternalUserClaimTypes.FullName, "Jane Doe"),
            new Claim(ClaimTypes.Email, "jane.doe@example.com"));

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    private static TempDataDictionary NewTempData() => new(new DefaultHttpContext(), new NullTempDataProvider());

    // The page that refused the user, with a request to refuse and the TempData it leaves the details in.
    private static (RefusingPage Page, TempDataDictionary TempData) CreateRefusingPage()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("d2r2.example");
        httpContext.Request.Path = RequestPath;
        httpContext.Request.QueryString = new QueryString("?section=3");
        var tempData = new TempDataDictionary(httpContext, new NullTempDataProvider());

        return (new RefusingPage { PageContext = new PageContext { HttpContext = httpContext }, TempData = tempData }, tempData);
    }

    private static ApplicationErrorModel CreatePageModel(TempDataDictionary tempData, ClaimsPrincipal user) =>
        new(new UserDisplayNameService(), new FixedTimeProvider(ErrorTime))
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = user } },
            TempData = tempData,
            Url = new SearchPageUrlHelper()
        };

    private sealed class RefusingPage : PageModel;

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    /// <summary>Resolves every page to the search page's path, which is the only page the error page links to.</summary>
    private sealed class SearchPageUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor());

        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();

        public string? Content(string? contentPath) => throw new NotSupportedException();

        public bool IsLocalUrl(string? url) => throw new NotSupportedException();

        public string? Link(string? routeName, object? values) => throw new NotSupportedException();

        public string? RouteUrl(UrlRouteContext routeContext) => (string?)new RouteValueDictionary(routeContext.Values)["page"];
    }
}
