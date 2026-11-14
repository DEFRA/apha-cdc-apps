using System.Security.Claims;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Claims;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CDC.Web.Tests.Features.Account;

public class InternalUserResolverTests
{
    private static readonly Guid SsoUserIdInt = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ClaimsPrincipal CreatePrincipal(
        string? oid = "22222222-2222-2222-2222-222222222222",
        string? samAccountName = "jdoe",
        string? domainName = "DEFRA",
        string? name = "Jane Internal")
    {
        var claims = new List<Claim>();
        if (oid is not null)
        {
            claims.Add(new Claim(EntraClaimTypes.ObjectId, oid));
        }
        if (samAccountName is not null)
        {
            claims.Add(new Claim(EntraClaimTypes.OnPremisesSamAccountName, samAccountName));
        }
        if (domainName is not null)
        {
            claims.Add(new Claim(EntraClaimTypes.OnPremisesDomainName, domainName));
        }
        if (name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public async Task ResolveAsync_Allows_AndAddsClaims_OnSuccess()
    {
        var principal = CreatePrincipal();
        var user = new InternalUserDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Jane Internal",
            IsProfileEditor = true,
            IsPolicyProfileUser = false
        };
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Success, user));

        var resolution = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.True(resolution.IsAllowed);
        Assert.Equal(user.Id.ToString(), resolution.Claims![ExternalUserClaimTypes.InternalUserId]);
        Assert.Equal("Jane Internal", resolution.Claims[ExternalUserClaimTypes.FullName]);
        Assert.Equal(EntraAuthenticationDefaults.AuthenticationScheme, resolution.Claims[ExternalUserClaimTypes.AuthenticationProvider]);
        Assert.Equal("True", resolution.Claims[ExternalUserClaimTypes.IsProfileEditor]);
        Assert.Equal("False", resolution.Claims[ExternalUserClaimTypes.IsPolicyProfileUser]);
        Assert.False(resolution.Claims.ContainsKey(ClaimTypes.Role));

        Assert.NotNull(apiClient.LastRequest);
        Assert.Equal(SsoUserIdInt, apiClient.LastRequest!.SsoUserIdInt);
        Assert.Equal(@"DEFRA\jdoe", apiClient.LastRequest.UserName);
        Assert.Equal("Jane Internal", apiClient.LastRequest.FullName);
    }

    [Fact]
    public async Task ResolveAsync_SendsNoEmail_WhenTheTokenCarriesNone()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(CreatePrincipal(), CancellationToken.None);

        Assert.Null(apiClient.LastRequest!.Email);
    }

    [Theory]
    [InlineData("email", "jane@example.test", "jane@example.test")]
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", " jane@example.test ", "jane@example.test")]
    [InlineData("upn", "jane@upn.example.test", "jane@upn.example.test")]
    [InlineData("preferred_username", "jane@signin.example.test", "jane@signin.example.test")]
    [InlineData("preferred_username", "jdoe", null)]
    [InlineData("email", "   ", null)]
    public async Task ResolveAsync_SendsTheEmailFromTheTokenOnlyWhenItLooksLikeAnEmailAddress(string claimType, string claimValue, string? expected)
    {
        var principal = CreatePrincipal();
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(claimType, claimValue));
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.Equal(expected, apiClient.LastRequest!.Email);
    }

    [Fact]
    public async Task ResolveAsync_PrefersTheEmailClaimOverTheSignInName()
    {
        var principal = CreatePrincipal();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim("preferred_username", "signin@example.test"));
        identity.AddClaim(new Claim("email", "mail@example.test"));
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("mail@example.test", apiClient.LastRequest!.Email);
    }

    [Fact]
    public async Task ResolveAsync_Allows_WithNoRole_WhenApiReturnsALimitedAccessUser()
    {
        var principal = CreatePrincipal();
        var user = new InternalUserDto
        {
            Id = Guid.Empty,
            FullName = "Jane Internal",
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Success, user));

        var resolution = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.True(resolution.IsAllowed);
        Assert.Equal(Guid.Empty.ToString(), resolution.Claims![ExternalUserClaimTypes.InternalUserId]);
        Assert.Equal("False", resolution.Claims[ExternalUserClaimTypes.IsProfileEditor]);
        Assert.Equal("False", resolution.Claims[ExternalUserClaimTypes.IsPolicyProfileUser]);
        Assert.False(resolution.Claims.ContainsKey(ClaimTypes.Role));
    }

    [Fact]
    public async Task ResolveAsync_DeniesToNotPermittedPage_WhenApiReturnsNotPermitted()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.NotPermitted, null));

        var resolution = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_DeniesToGenericErrorPage_WhenApiCallFails()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        var resolution = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Landing/Error", resolution.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenOidClaimIsMissingOrNotAGuid()
    {
        var principal = CreatePrincipal(oid: "not-a-guid");
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        var resolution = await new InternalUserResolver(apiClient, logger).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
        Assert.Contains(logger.Messages, message => message.Contains("no usable object id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenNoUserNameClaimsArePresent()
    {
        var principal = CreatePrincipal(samAccountName: null, domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        var resolution = await new InternalUserResolver(apiClient, logger).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
        Assert.Contains(
            logger.Messages,
            message => message.Contains($"SsoUserIdInt={SsoUserIdInt}", StringComparison.Ordinal)
                && message.Contains("onprem_samaccountname", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_LogsTheOid_EvenWhenSignInIsDeniedForMissingUserName()
    {
        var principal = CreatePrincipal(samAccountName: null, domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        await new InternalUserResolver(apiClient, logger).ResolveAsync(principal, CancellationToken.None);

        Assert.Contains(logger.Messages, message => message.Contains("received for object id", StringComparison.Ordinal)
            && message.Contains(SsoUserIdInt.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_UsesSamAccountNameAlone_WhenDomainNameClaimIsMissing()
    {
        var principal = CreatePrincipal(domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("jdoe", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_UsesTheDirectoryEmployeeId_WhenTheTokenHasNoAccountClaims()
    {
        var principal = CreatePrincipal(samAccountName: null, domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var graph = new FakeGraphClient { EmployeeId = "12345" };

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, graphClient: graph).ResolveAsync(principal, CancellationToken.None);

        Assert.Equal(SsoUserIdInt, graph.RequestedObjectId);
        Assert.Equal(@"DEMETER\12345", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_PrefersTheDirectoryEmployeeId_ToABareAccountName()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var graph = new FakeGraphClient { EmployeeId = "12345" };

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, graphClient: graph)
            .ResolveAsync(CreatePrincipal(domainName: null), CancellationToken.None);

        Assert.Equal(@"DEMETER\12345", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotQueryTheDirectory_WhenTheTokenGivesADomainQualifiedName()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var graph = new FakeGraphClient { EmployeeId = "12345" };

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, graphClient: graph).ResolveAsync(CreatePrincipal(), CancellationToken.None);

        Assert.Null(graph.RequestedObjectId);
        Assert.Equal(@"DEFRA\jdoe", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_KeepsTheBareAccountName_WhenTheDirectoryHasNoEmployeeId()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var graph = new FakeGraphClient();

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, graphClient: graph)
            .ResolveAsync(CreatePrincipal(domainName: null), CancellationToken.None);

        Assert.Equal("jdoe", apiClient.LastRequest!.UserName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData(@"12\34")]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenTheDirectoryEmployeeIdIsMissingOrUnusable(string? employeeId)
    {
        var principal = CreatePrincipal(samAccountName: null, domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        var resolution = await new InternalUserResolver(apiClient, logger, graphClient: new FakeGraphClient { EmployeeId = employeeId }).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
        Assert.Contains(logger.Messages, message => message.Contains("No usable employee id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_DeniesAndLogs_WhenTheDirectoryLookupFailsAndTheTokenHasNoAccountName()
    {
        var principal = CreatePrincipal(samAccountName: null, domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        var resolution = await new InternalUserResolver(apiClient, logger, graphClient: new FakeGraphClient { ThrowOnEmployeeId = true }).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
        Assert.Contains(logger.Messages, message => message.Contains("employee id lookup failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToTheBareAccountName_WhenTheDirectoryLookupFails()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, graphClient: new FakeGraphClient { ThrowOnEmployeeId = true })
            .ResolveAsync(CreatePrincipal(domainName: null), CancellationToken.None);

        Assert.Equal("jdoe", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_TruncatesDomainNameAtFirstDot_WhenAdConnectSyncsTheFqdnForm()
    {
        var principal = CreatePrincipal(domainName: "DT2.local");
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.Equal(@"DT2\jdoe", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_ReadsOid_WhenJwtInboundMappingRemapsItToTheLongClaimUri()
    {
        var claims = new List<Claim>
        {
            new(EntraClaimTypes.ObjectIdLongClaimUri, SsoUserIdInt.ToString()),
            new(EntraClaimTypes.OnPremisesSamAccountName, "jdoe"),
            new(EntraClaimTypes.OnPremisesDomainName, "DEFRA")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.NotNull(apiClient.LastRequest);
        Assert.Equal(SsoUserIdInt, apiClient.LastRequest!.SsoUserIdInt);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenPrincipalIsNull()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        var act = async () => await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(null!, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task ResolveAsync_DeniesToGenericErrorPage_WhenApiCallThrowsHttpRequestException()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null), new HttpRequestException("Connection refused"));

        var resolution = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Landing/Error", resolution.DenialRedirectPath);
    }

    // The resilience handler gives up with its own Polly exceptions, which are neither an HttpRequestException nor a TaskCanceledException.
    [Fact]
    public Task ResolveAsync_DeniesAndLogsTheFailure_WhenTheResilienceHandlerTimesOut() =>
        AssertDeniedAndLoggedAsync(new TimeoutRejectedException("The operation didn't complete within the allowed timeout of '00:00:30'.", new TaskCanceledException()));

    [Fact]
    public Task ResolveAsync_DeniesAndLogsTheFailure_WhenTheResilienceHandlersCircuitIsOpen() =>
        AssertDeniedAndLoggedAsync(new BrokenCircuitException("The circuit is now open and is not allowing calls."));

    // What a sign-in sees when CDC.Api cannot be reached: the real pipeline's timeout, not a hand-built exception.
    [Fact]
    public async Task ResolveAsync_DeniesAndLogsTheFailure_WhenARealResiliencePipelineTimesOutWaitingForCdcApi()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<IApiClient, ApiClient>(client => client.BaseAddress = new Uri("http://cdc-api.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new NeverAnsweringHandler())
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(500);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
            });
        await using var provider = services.BuildServiceProvider();
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        var resolution = await new InternalUserResolver(provider.GetRequiredService<IApiClient>(), logger).ResolveAsync(CreatePrincipal(), CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Landing/Error", resolution.DenialRedirectPath);
        Assert.Contains(logger.Messages, message => message.Contains("Failed to resolve internal user", StringComparison.Ordinal));
    }

    private static async Task AssertDeniedAndLoggedAsync(Exception failure)
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null), failure);
        var logger = new AlwaysEnabledLogger<InternalUserResolver>();

        var resolution = await new InternalUserResolver(apiClient, logger).ResolveAsync(CreatePrincipal(), CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Landing/Error", resolution.DenialRedirectPath);
        Assert.Contains(logger.Messages, message => message.Contains($"Failed to resolve internal user (SsoUserIdInt={SsoUserIdInt})", StringComparison.Ordinal));
    }

    // A browser holds one sign-in at a time: signing in with Entra ID ends the external (CIDM) session.
    [Fact]
    public async Task ResolveAsync_EndsTheExternalSession_OnSuccess()
    {
        var user = new InternalUserDto { Id = Guid.NewGuid(), FullName = "Jane Internal", IsProfileEditor = true, IsPolicyProfileUser = false };
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Success, user));
        var (accessor, authService) = FakeAuthenticationService.CreateAccessor();

        await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, accessor).ResolveAsync(CreatePrincipal(), CancellationToken.None);

        Assert.Equal([CookieAuthenticationDefaults.AuthenticationScheme], authService.SignOutSchemes);
    }

    [Theory]
    [InlineData(ResolveInternalUserOutcome.NotPermitted)]
    [InlineData(ResolveInternalUserOutcome.Error)]
    public async Task ResolveAsync_LeavesTheExternalSessionAlone_WhenSignInIsDenied(ResolveInternalUserOutcome outcome)
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(outcome, null));
        var (accessor, authService) = FakeAuthenticationService.CreateAccessor();

        var resolution = await new InternalUserResolver(apiClient, NullLogger<InternalUserResolver>.Instance, accessor).ResolveAsync(CreatePrincipal(), CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Empty(authService.SignOutCalls);
    }

    // Holds every request until the caller gives up, as a CDC.Api whose address silently drops connections does.
    private sealed class NeverAnsweringHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new HttpResponseMessage();
        }
    }

    // Test double for IApiClient recording the last resolve request and returning a scripted result
    // (or throwing a scripted exception, to exercise ResolveAsync's catch branch).
    private sealed class StubApiClient(ResolveInternalUserResult scriptedResult, Exception? thrownException = null) : IApiClient
    {
        public ResolveInternalUserRequestDto? LastRequest { get; private set; }

        public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ApiHealthResponse?>(null);

        public Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
            string? searchText,
            bool displayPublished,
            bool displayDraft,
            bool displayScenarios,
            SearchForType searchForType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProfileSearchResultDto>>([]);

        public Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProfileAttributesDto?>(null);

        public Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
            Guid profileId,
            string title,
            byte[] lastUpdated,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Error, "Not implemented in this fake."));

        public Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ManageProfileViewModel?>(null);

        public Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, Guid? profileVersionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ManageProfileViewModel?>(null);

        public Task<ResolveExternalUserResult> ResolveExternalUserAsync(
            ResolveExternalUserRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

        public Task<ResolveInternalUserResult> ResolveInternalUserAsync(
            ResolveInternalUserRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return thrownException is not null
                ? throw thrownException
                : Task.FromResult(scriptedResult);
        }

        public Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProfileStatusTypeDto>>([]);

        public Task<UpdateProfileStatusResult> UpdateProfileStatusAsync(
            Guid profileId,
            Guid profileStatusId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Error, "Not implemented in this fake."));

        public Task<IReadOnlyList<StaticReportListItemDto>> GetCurrentStaticReportsAsync(
            bool isUserManual = false,
            bool publicOnly = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StaticReportListItemDto>>([]);

        public Task<IReadOnlyList<StaticReportListItemDto>> GetStaticReportHistoryAsync(
            Guid staticReportId,
            bool publicOnly = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StaticReportListItemDto>>([]);

        public Task<bool> CanUploadStaticReportsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<UploadStaticReportResult> UploadStaticReportAsync(
            string title,
            byte[] pdfData,
            bool isUserManual,
            bool isPublic,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UploadStaticReportResult(UploadStaticReportOutcome.Forbidden, "Not implemented in this fake."));

        public Task<bool> SetStaticReportPublicAccessAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<DeleteStaticReportVersionResult> DeleteStaticReportVersionAsync(
            Guid staticReportVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeleteStaticReportVersionResult(DeleteStaticReportVersionOutcome.Error, "Not implemented in this fake."));

        public Task<CreateNewProfileVersionResult> CreateNewProfileVersionAsync(
            Guid profileVersionId,
            bool isPublished,
            bool isPublic,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Error, null, "Not implemented in this fake."));

        public Task<PublishProfileResult> PublishProfileAsync(Guid profileId, bool isPublic, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PublishProfileResult(PublishProfileOutcome.Error, null, "Not implemented in this fake."));

        public Task<DeleteProfileVersionResult> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Error, false, "Not implemented in this fake."));

        public Task<bool> SetProfileVersionPublicAccessAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
