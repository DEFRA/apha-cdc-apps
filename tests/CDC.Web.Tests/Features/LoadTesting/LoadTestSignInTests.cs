using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Models;
using CDC.Web.Tests.Integration;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CDC.Web.Tests.Features.LoadTesting;

public class LoadTestSignInTests
{
    private static (LoadTestSignIn SignIn, FakeApiClient Api, DefaultHttpContext Context, FakeAuthenticationService Authentication) Create(bool enabled = true)
    {
        var api = new FakeApiClient();
        var status = new LoadTestingStatus(Options.Create(new LoadTestingOptions { Enabled = enabled }), new ControllableClock(DateTimeOffset.UtcNow));
        var authentication = new FakeAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };

        return (new LoadTestSignIn(api, status, NullLogger<LoadTestSignIn>.Instance), api, context, authentication);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsActive_FollowsTheStatus(bool enabled)
    {
        var (signIn, _, _, _) = Create(enabled);

        Assert.Equal(enabled, signIn.IsActive);
    }

    [Theory]
    [InlineData(UserLookupKind.Internal)]
    [InlineData(UserLookupKind.External)]
    public async Task AsksCdcApiForTheUserByTheTrimmedEmailAddressForTheKindOfSignInChosen(UserLookupKind kind)
    {
        var (signIn, api, context, _) = Create();

        await signIn.SignInAsync(context, kind, "  someone@example.test ");

        Assert.Equal((kind, "someone@example.test"), Assert.Single(api.UserLookups));
    }

    [Fact]
    public async Task Internal_SignsTheUserFoundIntoTheEntraCookieWithTheClaimsARealSignInGives()
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => LoadTestUsers.Found(LoadTestUsers.Internal(isProfileEditor: true, isPolicyProfileUser: false));

        var outcome = await signIn.SignInAsync(context, UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress);

        Assert.Equal(LoadTestSignInOutcome.SignedIn, outcome);
        var (scheme, principal) = Assert.Single(authentication.SignInCalls);
        Assert.Equal(EntraAuthenticationDefaults.CookieAuthenticationScheme, scheme);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(LoadTestUsers.InternalUserId.ToString(), principal.FindFirst("internalUserId")?.Value);
        Assert.Equal("Load Test Internal", principal.FindFirst("fullName")?.Value);
        Assert.Equal(EntraAuthenticationDefaults.AuthenticationScheme, principal.FindFirst("authenticationProvider")?.Value);
        Assert.Equal(bool.TrueString, principal.FindFirst("isProfileEditor")?.Value);
        Assert.Equal(bool.FalseString, principal.FindFirst("isPolicyProfileUser")?.Value);
        Assert.Equal(LoadTestUsers.InternalUserId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Load Test Internal", principal.Identity.Name);
        Assert.True(principal.HasClaim("loadTestSession", bool.TrueString));
    }

    [Fact]
    public async Task Internal_GivesThePermissionFlagsOfTheUsersOwnRecord()
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => LoadTestUsers.Found(LoadTestUsers.Internal(isProfileEditor: false, isPolicyProfileUser: true));

        await signIn.SignInAsync(context, UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress);

        var principal = Assert.Single(authentication.SignInCalls).Principal;
        Assert.Equal(bool.FalseString, principal.FindFirst("isProfileEditor")?.Value);
        Assert.Equal(bool.TrueString, principal.FindFirst("isPolicyProfileUser")?.Value);
    }

    [Fact]
    public async Task External_SignsTheUserFoundIntoTheCidmCookieWithTheClaimsARealSignInGives()
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => LoadTestUsers.Found(LoadTestUsers.External());

        var outcome = await signIn.SignInAsync(context, UserLookupKind.External, LoadTestUsers.ExternalEmailAddress);

        Assert.Equal(LoadTestSignInOutcome.SignedIn, outcome);
        var (scheme, principal) = Assert.Single(authentication.SignInCalls);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(LoadTestUsers.ExternalUserId.ToString(), principal.FindFirst("internalUserId")?.Value);
        Assert.Equal("Load Test External", principal.FindFirst("fullName")?.Value);
        Assert.Equal(CidmAuthenticationDefaults.AuthenticationScheme, principal.FindFirst("authenticationProvider")?.Value);
        Assert.Equal(LoadTestUsers.ExternalUserId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.False(principal.HasClaim(claim => claim.Type == "isInternalProfileEditor"));
        Assert.True(principal.HasClaim("loadTestSession", bool.TrueString));
    }

    [Fact]
    public async Task External_MarksAnInternalProfileEditorWhoSignsInThroughCidm()
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => LoadTestUsers.Found(LoadTestUsers.External(isInternalProfileEditor: true));

        await signIn.SignInAsync(context, UserLookupKind.External, LoadTestUsers.ExternalEmailAddress);

        Assert.True(Assert.Single(authentication.SignInCalls).Principal.HasClaim("isInternalProfileEditor", bool.TrueString));
    }

    [Theory]
    [InlineData(UserLookupOutcome.NotFound, LoadTestSignInOutcome.NotFound)]
    [InlineData(UserLookupOutcome.NotPermitted, LoadTestSignInOutcome.NotPermitted)]
    [InlineData(UserLookupOutcome.Ambiguous, LoadTestSignInOutcome.Ambiguous)]
    [InlineData(UserLookupOutcome.InvalidEmailAddress, LoadTestSignInOutcome.InvalidEmailAddress)]
    [InlineData(UserLookupOutcome.Error, LoadTestSignInOutcome.Unavailable)]
    public async Task WhenCdcApiDoesNotFindAUsableUser_SignsNobodyInAndSaysWhy(UserLookupOutcome lookup, LoadTestSignInOutcome expected)
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => LoadTestUsers.Refused(lookup);

        var outcome = await signIn.SignInAsync(context, UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress);

        Assert.Equal(expected, outcome);
        Assert.Empty(authentication.SignInCalls);
    }

    [Fact]
    public async Task AFoundResultWithNoUser_IsTreatedAsUnavailableAndSignsNobodyIn()
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => new UserLookupResult(UserLookupOutcome.Found, null);

        var outcome = await signIn.SignInAsync(context, UserLookupKind.External, LoadTestUsers.ExternalEmailAddress);

        Assert.Equal(LoadTestSignInOutcome.Unavailable, outcome);
        Assert.Empty(authentication.SignInCalls);
    }

    public static TheoryData<Exception> ConnectivityFailures => new()
    {
        new HttpRequestException("connection refused"),
        new TaskCanceledException("cancelled"),
        new NotSupportedException("not JSON"),
        new TimeoutRejectedException("timed out"),
        new BrokenCircuitException("the circuit is open")
    };

    [Theory]
    [MemberData(nameof(ConnectivityFailures))]
    public async Task WhenCdcApiCannotBeReached_IsUnavailableAndSignsNobodyIn(Exception failure)
    {
        var (signIn, api, context, authentication) = Create();
        api.LookUpUser = (_, _) => throw failure;

        var outcome = await signIn.SignInAsync(context, UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress);

        Assert.Equal(LoadTestSignInOutcome.Unavailable, outcome);
        Assert.Empty(authentication.SignInCalls);
    }

    [Fact]
    public async Task AFailureThatIsNotAboutReachingCdcApi_IsNotHidden()
    {
        var (signIn, api, context, _) = Create();
        api.LookUpUser = (_, _) => throw new InvalidOperationException("a bug");

        await Assert.ThrowsAsync<InvalidOperationException>(() => signIn.SignInAsync(context, UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress));
    }

    [Fact]
    public async Task RefusesANullRequestAndAnEmptyEmailAddress()
    {
        var (signIn, _, context, _) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(() => signIn.SignInAsync(null!, UserLookupKind.Internal, LoadTestUsers.InternalEmailAddress));
        await Assert.ThrowsAsync<ArgumentNullException>(() => signIn.SignInAsync(context, UserLookupKind.Internal, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => signIn.SignInAsync(context, UserLookupKind.Internal, "   "));
    }
}
