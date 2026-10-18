using System.Net;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

public class AccountSmokeTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public AccountSmokeTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_Get_RedirectsToCidmAuthorizeEndpointRatherThanRenderingAForm()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task LoginInternal_Get_RedirectsToEntraAuthorizeEndpointRatherThanRenderingAForm()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Account/LoginInternal");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task SignedOut_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/SignedOut");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NotPermitted_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/NotPermitted");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The page the cookie schemes redirect a signed-in user to when a policy refuses them a page.
    [Fact]
    public async Task AccessDenied_ReturnsForbiddenWithAnExplanation()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/AccessDenied");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("You do not have permission to view this page", await response.Content.ReadAsStringAsync());
    }

    // CIDM's B2C policy doesn't always echo back the protected state on its post-logout redirect,
    // so the callback must still land somewhere real rather than rendering an empty response.
    [Fact]
    public async Task SignOutOidcCallback_RedirectsToSignedOutPage_WhenNoStateIsEchoedBack()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/signout-oidc");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/SignedOut", response.Headers.Location!.ToString());
    }
}
