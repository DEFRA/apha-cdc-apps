using System.Security.Claims;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Claims;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

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

        var resolution = await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.True(resolution.IsAllowed);
        Assert.Equal(user.Id.ToString(), resolution.Claims![ExternalUserClaimTypes.InternalUserId]);
        Assert.Equal("Jane Internal", resolution.Claims[ExternalUserClaimTypes.FullName]);
        Assert.Equal(EntraAuthenticationDefaults.AuthenticationScheme, resolution.Claims[ExternalUserClaimTypes.AuthenticationProvider]);
        Assert.Equal("True", resolution.Claims[ExternalUserClaimTypes.IsProfileEditor]);
        Assert.Equal("False", resolution.Claims[ExternalUserClaimTypes.IsPolicyProfileUser]);

        Assert.NotNull(apiClient.LastRequest);
        Assert.Equal(SsoUserIdInt, apiClient.LastRequest!.SsoUserIdInt);
        Assert.Equal(@"DEFRA\jdoe", apiClient.LastRequest.UserName);
        Assert.Equal("Jane Internal", apiClient.LastRequest.FullName);
    }

    [Fact]
    public async Task ResolveAsync_Allows_WithNoPrivilegeClaims_WhenApiReturnsALimitedAccessUser()
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

        var resolution = await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.True(resolution.IsAllowed);
        Assert.Equal(Guid.Empty.ToString(), resolution.Claims![ExternalUserClaimTypes.InternalUserId]);
        Assert.Equal("False", resolution.Claims[ExternalUserClaimTypes.IsProfileEditor]);
        Assert.Equal("False", resolution.Claims[ExternalUserClaimTypes.IsPolicyProfileUser]);
    }

    [Fact]
    public async Task ResolveAsync_DeniesToNotPermittedPage_WhenApiReturnsNotPermitted()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.NotPermitted, null));

        var resolution = await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_DeniesToGenericErrorPage_WhenApiCallFails()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        var resolution = await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Landing/Error", resolution.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenOidClaimIsMissingOrNotAGuid()
    {
        var principal = CreatePrincipal(oid: "not-a-guid");
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        var resolution = await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
    }

    [Fact]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenNoUserNameClaimsArePresent()
    {
        var principal = CreatePrincipal(samAccountName: null, domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        var resolution = await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
    }

    [Fact]
    public async Task ResolveAsync_UsesSamAccountNameAlone_WhenDomainNameClaimIsMissing()
    {
        var principal = CreatePrincipal(domainName: null);
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.Equal("jdoe", apiClient.LastRequest!.UserName);
    }

    [Fact]
    public async Task ResolveAsync_TruncatesDomainNameAtFirstDot_WhenAdConnectSyncsTheFqdnForm()
    {
        var principal = CreatePrincipal(domainName: "DT2.local");
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

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

        await new InternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.NotNull(apiClient.LastRequest);
        Assert.Equal(SsoUserIdInt, apiClient.LastRequest!.SsoUserIdInt);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenPrincipalIsNull()
    {
        var apiClient = new StubApiClient(new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null));

        var act = async () => await new InternalUserResolver(apiClient).ResolveAsync(null!, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    // Test double for IApiClient recording the last resolve request and returning a scripted result.
    private sealed class StubApiClient(ResolveInternalUserResult scriptedResult) : IApiClient
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

        public Task<ResolveExternalUserResult> ResolveExternalUserAsync(
            ResolveExternalUserRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

        public Task<ResolveInternalUserResult> ResolveInternalUserAsync(
            ResolveInternalUserRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(scriptedResult);
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

        public Task<CreateNewProfileVersionResult> CreateNewProfileVersionAsync(
            Guid profileVersionId,
            bool isPublished,
            bool isPublic,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Error, null, "Not implemented in this fake."));

        public Task<DeleteProfileVersionResult> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Error, false, "Not implemented in this fake."));
    }
}
