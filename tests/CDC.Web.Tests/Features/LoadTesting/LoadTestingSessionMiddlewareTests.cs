using System.Security.Claims;
using CDC.Auth.Entra;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Features.LoadTesting;

public class LoadTestingSessionMiddlewareTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal Principal(bool loadTestSession) =>
        new(new ClaimsIdentity(loadTestSession ? [new Claim("loadTestSession", bool.TrueString)] : [new Claim(ClaimTypes.Name, "someone")], "test"));

    private static async Task<(bool NextCalled, DefaultHttpContext Context, FakeAuthenticationService Authentication)> RunAsync(bool active, bool loadTestSession)
    {
        var clock = new ControllableClock(Now);
        var status = new LoadTestingStatus(Options.Create(new LoadTestingOptions { Enabled = true, ExpiresAtUtc = active ? Now.AddHours(1) : Now.AddHours(-1) }), clock);
        var authentication = new FakeAuthenticationService();
        var context = new DefaultHttpContext
        {
            User = Principal(loadTestSession),
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider()
        };
        var nextCalled = false;
        var middleware = new LoadTestingSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, status, NullLogger<LoadTestingSessionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        return (nextCalled, context, authentication);
    }

    [Fact]
    public async Task WhileTheBypassIsActive_ALoadTestingSessionIsLeftAlone()
    {
        var (nextCalled, context, authentication) = await RunAsync(active: true, loadTestSession: true);

        Assert.True(nextCalled);
        Assert.Empty(authentication.SignOutCalls);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task AfterTheBypassEnds_ARealSessionIsLeftAlone()
    {
        var (nextCalled, _, authentication) = await RunAsync(active: false, loadTestSession: false);

        Assert.True(nextCalled);
        Assert.Empty(authentication.SignOutCalls);
    }

    [Fact]
    public async Task AfterTheBypassEnds_ALoadTestingSessionIsEndedInBothCookiesAndSentHome()
    {
        var (nextCalled, context, authentication) = await RunAsync(active: false, loadTestSession: true);

        Assert.False(nextCalled);
        Assert.Equal(
            [CookieAuthenticationDefaults.AuthenticationScheme, EntraAuthenticationDefaults.CookieAuthenticationScheme],
            authentication.SignOutSchemes);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/", context.Response.Headers.Location.ToString());
    }
}
