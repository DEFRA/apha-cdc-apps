using System.Security.Claims;
using CDC.Auth.Entra.Events;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Auth.Entra.Tests.Events;

public class EntraOpenIdConnectEventsTests
{
    private static EntraOpenIdConnectEvents CreateEvents() => new(NullLogger<EntraOpenIdConnectEvents>.Instance);

    private static AuthenticationScheme CreateScheme() =>
        new("entra", "entra", typeof(OpenIdConnectHandler));

    [Fact]
    public async Task TokenValidated_PrincipalWithoutClaimsIdentity_ReturnsWithoutThrowing()
    {
        var principal = new ClaimsPrincipal();
        var context = new TokenValidatedContext(
            new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        var exception = await Record.ExceptionAsync(() => CreateEvents().TokenValidated(context));

        Assert.Null(exception);
        Assert.Empty(principal.Claims);
    }

    [Fact]
    public async Task TokenValidated_NoResolverRegistered_LeavesPrincipalUnchanged()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var context = new TokenValidatedContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        await CreateEvents().TokenValidated(context);

        Assert.False(context.Result?.Handled ?? false);
        Assert.DoesNotContain(identity.Claims, claim => claim.Type == "internalUserId");
    }

    [Fact]
    public async Task TokenValidated_ResolverAllows_AddsReturnedClaims()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new StubResolver(EntraInternalUserResolution.Allow(new Dictionary<string, string>
        {
            ["internalUserId"] = "11111111-1111-1111-1111-111111111111",
            ["fullName"] = "Jane Internal"
        }));
        var services = new ServiceCollection();
        services.AddSingleton<IEntraInternalUserResolver>(resolver);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var context = new TokenValidatedContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        await CreateEvents().TokenValidated(context);

        Assert.Contains(identity.Claims, c => c.Type == "internalUserId" && c.Value == "11111111-1111-1111-1111-111111111111");
        Assert.Contains(identity.Claims, c => c.Type == "fullName" && c.Value == "Jane Internal");
    }

    [Fact]
    public async Task TokenValidated_ResolverDenies_HandlesResponseAndRedirectsToDenialPath()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new StubResolver(EntraInternalUserResolution.Deny("/Account/NotPermitted"));
        var services = new ServiceCollection();
        services.AddSingleton<IEntraInternalUserResolver>(resolver);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        var context = new TokenValidatedContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        await CreateEvents().TokenValidated(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/Account/NotPermitted", httpContext.Response.Headers.Location);
    }

    [Fact]
    public async Task TicketReceived_RemovesTheAccessToken_AndKeepsTheOthers()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "access" },
            new AuthenticationToken { Name = "id_token", Value = "id" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh" },
            new AuthenticationToken { Name = "expires_at", Value = "2026-10-13T15:00:00.0000000+00:00" }
        ]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), properties, "entra");
        var context = new TicketReceivedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), ticket);

        await CreateEvents().TicketReceived(context);

        Assert.Null(properties.GetTokenValue("access_token"));
        Assert.Equal("id", properties.GetTokenValue("id_token"));
        Assert.Equal("refresh", properties.GetTokenValue("refresh_token"));
        Assert.Equal("2026-10-13T15:00:00.0000000+00:00", properties.GetTokenValue("expires_at"));
    }

    [Fact]
    public async Task TicketReceived_WithNoTokens_DoesNotThrow()
    {
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), new AuthenticationProperties(), "entra");
        var context = new TicketReceivedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), ticket);

        var exception = await Record.ExceptionAsync(() => CreateEvents().TicketReceived(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RemoteFailure_HandlesResponseAndRedirectsToGenericErrorPage()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RemoteFailureContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), new InvalidOperationException("boom"));

        await CreateEvents().RemoteFailure(context);

        Assert.True(context.Result.Handled);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/Landing/Error", httpContext.Response.Headers.Location.ToString());
    }

    private sealed class StubResolver(EntraInternalUserResolution resolution) : IEntraInternalUserResolver
    {
        public Task<EntraInternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            Task.FromResult(resolution);
    }
}
