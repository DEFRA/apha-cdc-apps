using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Claims;
using CDC.Auth.Entra.Events;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Features.LoadTesting;

public class LoadTestSignInTests
{
    private static readonly Guid InternalSsoId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ExternalSsoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly LoadTestingOptions Settings = new()
    {
        Enabled = true,
        InternalUserName = "internal.user",
        InternalSsoUserId = InternalSsoId,
        ExternalUserEmail = "external.user@example.test",
        ExternalSsoUserId = ExternalSsoId
    };

    private static (LoadTestSignIn SignIn, FakeIdentityResolvers Resolvers, DefaultHttpContext Context, FakeAuthenticationService Authentication) Create(bool enabled = true)
    {
        var resolvers = new FakeIdentityResolvers();
        var options = Options.Create(Settings);
        var status = new LoadTestingStatus(Options.Create(new LoadTestingOptions { Enabled = enabled }), new ControllableClock(DateTimeOffset.UtcNow));
        var authentication = new FakeAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };

        return (new LoadTestSignIn(options, status, resolvers, resolvers, NullLogger<LoadTestSignIn>.Instance), resolvers, context, authentication);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsActive_FollowsTheStatus(bool enabled)
    {
        var (signIn, _, _, _) = Create(enabled);

        Assert.Equal(enabled, signIn.IsActive);
    }

    [Fact]
    public async Task Internal_HandsTheResolverTheClaimsOfAnEntraIdToken()
    {
        var (signIn, resolvers, context, _) = Create();

        await signIn.SignInInternalAsync(context);

        var token = resolvers.InternalToken!;
        Assert.Equal(InternalSsoId.ToString(), token.FindFirst(EntraClaimTypes.ObjectId)?.Value);
        Assert.Equal("internal.user", token.FindFirst(EntraClaimTypes.OnPremisesSamAccountName)?.Value);
        Assert.Null(token.FindFirst(EntraClaimTypes.OnPremisesDomainName));
    }

    [Fact]
    public async Task Internal_SignsTheResolvedUserIntoTheEntraCookieWithTheClaimsARealSignInGives()
    {
        var (signIn, _, context, authentication) = Create();

        var result = await signIn.SignInInternalAsync(context);

        Assert.True(result.Succeeded);
        var (scheme, principal) = Assert.Single(authentication.SignInCalls);
        Assert.Equal(EntraAuthenticationDefaults.CookieAuthenticationScheme, scheme);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("11111111-1111-1111-1111-111111111111", principal.FindFirst("internalUserId")?.Value);
        Assert.Equal("Load Test Internal", principal.FindFirst("fullName")?.Value);
        Assert.Equal(EntraAuthenticationDefaults.AuthenticationScheme, principal.FindFirst("authenticationProvider")?.Value);
        Assert.Equal(bool.TrueString, principal.FindFirst("isProfileEditor")?.Value);
        Assert.Equal(bool.FalseString, principal.FindFirst("isPolicyProfileUser")?.Value);
        Assert.Equal(InternalSsoId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Load Test Internal", principal.Identity.Name);
        Assert.True(principal.HasClaim("loadTestSession", bool.TrueString));
    }

    [Fact]
    public async Task Internal_WithNoFullNameFromTheUserRecord_ShowsTheConfiguredUserName()
    {
        var (signIn, resolvers, context, authentication) = Create();
        resolvers.InternalResolution = EntraInternalUserResolution.Allow(new Dictionary<string, string> { ["internalUserId"] = "11111111-1111-1111-1111-111111111111" });

        await signIn.SignInInternalAsync(context);

        Assert.Equal("internal.user", Assert.Single(authentication.SignInCalls).Principal.Identity!.Name);
    }

    [Fact]
    public async Task Internal_WithNoClaimsFromTheResolver_StillSignsInAMarkedSession()
    {
        var (signIn, resolvers, context, authentication) = Create();
        resolvers.InternalResolution = EntraInternalUserResolution.Allow();

        var result = await signIn.SignInInternalAsync(context);

        Assert.True(result.Succeeded);
        Assert.True(Assert.Single(authentication.SignInCalls).Principal.HasClaim("loadTestSession", bool.TrueString));
    }

    [Fact]
    public async Task Internal_WhenTheResolverRefuses_SignsNobodyInAndSaysWhereToGo()
    {
        var (signIn, resolvers, context, authentication) = Create();
        resolvers.InternalResolution = EntraInternalUserResolution.Deny("/Account/NotPermitted");

        var result = await signIn.SignInInternalAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal("/Account/NotPermitted", result.DenialRedirectPath);
        Assert.Empty(authentication.SignInCalls);
    }

    [Fact]
    public async Task External_HandsTheResolverTheClaimsOfACidmIdToken()
    {
        var (signIn, resolvers, context, _) = Create();

        await signIn.SignInExternalAsync(context);

        var token = resolvers.ExternalToken!;
        Assert.Equal(ExternalSsoId.ToString(), token.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("external.user@example.test", token.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task External_SignsTheResolvedUserIntoTheCidmCookieWithTheClaimsARealSignInGives()
    {
        var (signIn, _, context, authentication) = Create();

        var result = await signIn.SignInExternalAsync(context);

        Assert.True(result.Succeeded);
        var (scheme, principal) = Assert.Single(authentication.SignInCalls);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("22222222-2222-2222-2222-222222222222", principal.FindFirst("internalUserId")?.Value);
        Assert.Equal("Load Test External", principal.FindFirst("fullName")?.Value);
        Assert.Equal(CidmAuthenticationDefaults.AuthenticationScheme, principal.FindFirst("authenticationProvider")?.Value);
        Assert.Equal(ExternalSsoId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.True(principal.HasClaim("loadTestSession", bool.TrueString));
    }

    [Fact]
    public async Task External_WhenTheResolverRefuses_SignsNobodyInAndSaysWhereToGo()
    {
        var (signIn, resolvers, context, authentication) = Create();
        resolvers.ExternalResolution = CidmExternalUserResolution.Deny("/Landing/Error");

        var result = await signIn.SignInExternalAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal("/Landing/Error", result.DenialRedirectPath);
        Assert.Empty(authentication.SignInCalls);
    }

    [Fact]
    public async Task ARefusalWithNoPath_SendsTheUserToTheSiteRoot()
    {
        var (signIn, resolvers, context, _) = Create();
        resolvers.ExternalResolution = new CidmExternalUserResolution { IsAllowed = false };

        var result = await signIn.SignInExternalAsync(context);

        Assert.Equal("/", result.DenialRedirectPath);
    }

    [Fact]
    public async Task BothSignInsRefuseANullRequest()
    {
        var (signIn, _, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(() => signIn.SignInInternalAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => signIn.SignInExternalAsync(null!));
    }
}
