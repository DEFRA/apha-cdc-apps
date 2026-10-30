using CDC.Auth.Entra.Options;
using CDC.Auth.Entra.TokenRefresh;
using CDC.Common.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CDC.Auth.Entra.Tests.TokenRefresh;

public class EntraCookieEventsTests
{
    private static readonly EntraOptions TestEntraOptions = new()
    {
        TenantId = "11111111-1111-1111-1111-111111111111",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        RefreshBeforeExpiry = TimeSpan.FromMinutes(5)
    };

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static EntraCookieEvents CreateEvents(IEntraTokenRefreshService refreshService) =>
        new(refreshService, Microsoft.Extensions.Options.Options.Create(TestEntraOptions), new FakeTimeProvider(Now), NullLogger<EntraCookieEvents>.Instance);

    private static CookieValidatePrincipalContext CreateContext(AuthenticationProperties properties, out FakeAuthenticationService authService, bool passive = false)
    {
        authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        var httpContext = CreateHttpContext(passive);
        httpContext.RequestServices = services.BuildServiceProvider();
        var scheme = new AuthenticationScheme(EntraAuthenticationDefaults.CookieAuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(), properties, scheme.Name);
        var context = new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(), ticket);

        // The context doesn't surface the exact AuthenticationProperties instance passed into the
        // ticket - copy the token items across directly onto the one it actually exposes.
        foreach (var (key, value) in properties.Items)
        {
            context.Properties.Items[key] = value;
        }

        return context;
    }

    private static AuthenticationProperties CreatePropertiesWithTokens(string expiresAt, string refreshToken)
    {
        // Mirrors what OpenIdConnectHandler's SaveTokens=true actually registers at sign-in time -
        // UpdateTokenValue is a no-op for any name that wasn't part of the initial token set.
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "original-access-token" },
            new AuthenticationToken { Name = "id_token", Value = "original-id-token" },
            new AuthenticationToken { Name = "expires_at", Value = expiresAt },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken }
        ]);
        return properties;
    }

    [Fact]
    public async Task ValidatePrincipal_NoTokenMetadata_LeavesPrincipalUnchanged()
    {
        var context = CreateContext(new AuthenticationProperties(), out _);
        var refreshService = new FakeTokenRefreshService(TokenRefreshResult.Failed);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.False(context.ShouldRenew);
        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    public async Task ValidatePrincipal_NotYetNearExpiry_DoesNotRefresh()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(30).ToString("o"), "refresh-token-value");
        var context = CreateContext(properties, out _);
        var refreshService = new FakeTokenRefreshService(TokenRefreshResult.Failed);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    public async Task ValidatePrincipal_NearExpiryAndRefreshSucceeds_UpdatesTokensAndRenews()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(2).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out _);
        var refreshService = new FakeTokenRefreshService(new TokenRefreshResult(true, "new-access", "new-id", "new-refresh", Now.AddHours(1)));

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.True(context.ShouldRenew);
        Assert.Equal("new-access", context.Properties.GetTokenValue("access_token"));
        Assert.Equal("new-id", context.Properties.GetTokenValue("id_token"));
        Assert.Equal("new-refresh", context.Properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task ValidatePrincipal_NearExpiryAndRefreshFails_RejectsPrincipalAndSignsOut()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out var authService);
        var refreshService = new FakeTokenRefreshService(TokenRefreshResult.Failed);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.False(context.ShouldRenew);
        Assert.Contains(EntraAuthenticationDefaults.CookieAuthenticationScheme, authService.SignOutSchemes);
    }

    [Fact]
    public async Task ValidatePrincipal_RefreshSuccessWithNoNewRefreshToken_KeepsOriginalRefreshToken()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "original-refresh-token");
        var context = CreateContext(properties, out _);
        var refreshService = new FakeTokenRefreshService(new TokenRefreshResult(true, "new-access", null, null, Now.AddHours(1)));

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Equal("original-refresh-token", context.Properties.GetTokenValue("refresh_token"));
    }

    // --- The session cookie as an idle timeout ----------------------------------------------------------------------------------------

    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(20);

    private static DefaultHttpContext CreateHttpContext(bool passive)
    {
        var httpContext = new DefaultHttpContext();
        if (passive)
        {
            httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new PassiveSessionRequestAttribute()), "session status"));
        }

        return httpContext;
    }

    // A session cookie issued 'sinceIssued' ago that was given 'lifetime', as the cookie handler describes it to its sliding-expiration event.
    private static CookieSlidingExpirationContext CreateSlidingContext(TimeSpan sinceIssued, TimeSpan lifetime, bool passive = false)
    {
        var issuedUtc = Now - sinceIssued;
        var properties = new AuthenticationProperties { IssuedUtc = issuedUtc, ExpiresUtc = issuedUtc + lifetime };
        var scheme = new AuthenticationScheme(EntraAuthenticationDefaults.CookieAuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(), properties, scheme.Name);

        return new CookieSlidingExpirationContext(
            CreateHttpContext(passive), scheme, new CookieAuthenticationOptions { ExpireTimeSpan = SessionTimeout }, ticket, sinceIssued, lifetime - sinceIssued);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(19)]
    public async Task CheckSlidingExpiration_ARequestAMinuteOrMoreAfterTheLastRenewal_RenewsTheSessionForAFullTimeoutFromNow(int minutesSinceIssued)
    {
        // The built-in rule would not renew a session less than halfway through its lifetime.
        var context = CreateSlidingContext(TimeSpan.FromMinutes(minutesSinceIssued), SessionTimeout);

        await CreateEvents(new FakeTokenRefreshService(TokenRefreshResult.Failed)).CheckSlidingExpiration(context);

        Assert.True(context.ShouldRenew);
        Assert.Equal(Now, context.Properties.IssuedUtc);
        Assert.Equal(Now + SessionTimeout, context.Properties.ExpiresUtc);
    }

    [Fact]
    public async Task CheckSlidingExpiration_ARequestSoonAfterTheLastRenewal_LeavesTheCookieAlone()
    {
        var context = CreateSlidingContext(TimeSpan.FromSeconds(59), SessionTimeout);

        await CreateEvents(new FakeTokenRefreshService(TokenRefreshResult.Failed)).CheckSlidingExpiration(context);

        Assert.False(context.ShouldRenew);
        Assert.Equal(Now.AddSeconds(-59), context.Properties.IssuedUtc);
        Assert.Equal(Now.AddSeconds(-59) + SessionTimeout, context.Properties.ExpiresUtc);
    }

    // A change to the timeout has to apply to sessions already open, not just to those signed in after it.
    [Fact]
    public async Task CheckSlidingExpiration_ASessionGivenAnotherLifetime_IsRebasedOnTheConfiguredTimeout()
    {
        var context = CreateSlidingContext(TimeSpan.FromMinutes(5), TimeSpan.FromDays(14));

        await CreateEvents(new FakeTokenRefreshService(TokenRefreshResult.Failed)).CheckSlidingExpiration(context);

        Assert.True(context.ShouldRenew);
        Assert.Equal(Now + SessionTimeout, context.Properties.ExpiresUtc);
    }

    // Asking how long a session has left must never be what keeps it alive.
    [Fact]
    public async Task CheckSlidingExpiration_APassiveRequest_NeverRenewsTheSession()
    {
        var context = CreateSlidingContext(TimeSpan.FromMinutes(15), SessionTimeout, passive: true);

        await CreateEvents(new FakeTokenRefreshService(TokenRefreshResult.Failed)).CheckSlidingExpiration(context);

        Assert.False(context.ShouldRenew);
        Assert.Equal(Now.AddMinutes(-15), context.Properties.IssuedUtc);
        Assert.Equal(Now.AddMinutes(5), context.Properties.ExpiresUtc);
    }

    // Exchanging the refresh token would mean re-issuing the cookie to keep the new one, which would extend the session.
    [Fact]
    public async Task ValidatePrincipal_APassiveRequest_ExchangesNoTokensEvenWhenTheyAreAboutToExpire()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out var authService, passive: true);
        var refreshService = new FakeTokenRefreshService(new TokenRefreshResult(true, "new-access", "new-id", "new-refresh", Now.AddHours(1)));

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Equal(0, refreshService.CallCount);
        Assert.NotNull(context.Principal);
        Assert.False(context.ShouldRenew);
        Assert.Equal("old-refresh-token", context.Properties.GetTokenValue("refresh_token"));
        Assert.Empty(authService.SignOutSchemes);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeTokenRefreshService(TokenRefreshResult result) : IEntraTokenRefreshService
    {
        public int CallCount { get; private set; }

        public Task<TokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public List<string?> SignOutSchemes { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutSchemes.Add(scheme);
            return Task.CompletedTask;
        }
    }
}
