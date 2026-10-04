using System.Security.Claims;
using CDC.Auth.Cidm.Events;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Features.Account;

public class ExternalUserResolverTests
{
    private static readonly Guid CidmSsoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ClaimsPrincipal CreatePrincipal(
        string? sub = "22222222-2222-2222-2222-222222222222",
        string? email = "user@example.com",
        string? firstName = "Jane",
        string? lastName = "External",
        string? relationship = null)
    {
        var claims = new List<Claim>();
        if (sub is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }
        if (firstName is not null)
        {
            claims.Add(new Claim("firstName", firstName));
        }
        if (lastName is not null)
        {
            claims.Add(new Claim("lastName", lastName));
        }
        if (relationship is not null)
        {
            claims.Add(new Claim("relationships", relationship));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public async Task ResolveAsync_Allows_AndAddsClaims_OnSuccess()
    {
        var principal = CreatePrincipal(
            relationship: "23950a2d-c37d-43da-9fcb-0a4ce9aa11ee:bc19305a-f9b6-ea11-a812-000d3ab4653d:ACME Ltd:0:Employee:0");
        var user = new ExternalUserDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Jane External",
            EmailAddress = "user@example.com",
            Organisation = "ACME Ltd"
        };
        var apiClient = new StubApiClient(new ResolveExternalUserResult(ResolveExternalUserOutcome.Success, user));

        var resolution = await new ExternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.True(resolution.IsAllowed);
        Assert.Equal(user.Id.ToString(), resolution.Claims![ExternalUserClaimTypes.InternalUserId]);
        Assert.Equal("Jane External", resolution.Claims[ExternalUserClaimTypes.FullName]);

        Assert.NotNull(apiClient.LastRequest);
        Assert.Equal(CidmSsoId, apiClient.LastRequest!.CidmSsoId);
        Assert.Equal("user@example.com", apiClient.LastRequest.Email);
        Assert.Equal("Jane", apiClient.LastRequest.FirstName);
        Assert.Equal("External", apiClient.LastRequest.LastName);
        Assert.Equal("ACME Ltd", apiClient.LastRequest.Organisation);
    }

    [Fact]
    public async Task ResolveAsync_DeniesToNotPermittedPage_WhenApiReturnsNotPermitted()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveExternalUserResult(ResolveExternalUserOutcome.NotPermitted, null));

        var resolution = await new ExternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_DeniesToGenericErrorPage_WhenApiCallFails()
    {
        var principal = CreatePrincipal();
        var apiClient = new StubApiClient(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

        var resolution = await new ExternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Landing/Error", resolution.DenialRedirectPath);
    }

    [Fact]
    public async Task ResolveAsync_DeniesWithoutCallingApi_WhenSubClaimIsMissingOrNotAGuid()
    {
        var principal = CreatePrincipal(sub: "not-a-guid");
        var apiClient = new StubApiClient(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

        var resolution = await new ExternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.False(resolution.IsAllowed);
        Assert.Equal("/Account/NotPermitted", resolution.DenialRedirectPath);
        Assert.Null(apiClient.LastRequest);
    }

    [Fact]
    public async Task ResolveAsync_UsesEmptyStringDefaults_WhenOptionalClaimsAreMissing()
    {
        var principal = CreatePrincipal(email: null, firstName: null, lastName: null);
        var apiClient = new StubApiClient(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

        await new ExternalUserResolver(apiClient).ResolveAsync(principal, CancellationToken.None);

        Assert.NotNull(apiClient.LastRequest);
        Assert.Equal(string.Empty, apiClient.LastRequest!.Email);
        Assert.Equal(string.Empty, apiClient.LastRequest.FirstName);
        Assert.Equal(string.Empty, apiClient.LastRequest.LastName);
        Assert.Equal(string.Empty, apiClient.LastRequest.Organisation);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenPrincipalIsNull()
    {
        var apiClient = new StubApiClient(new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null));

        var act = async () => await new ExternalUserResolver(apiClient).ResolveAsync(null!, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    // Test double for IApiClient recording the last resolve request and returning a scripted result.
    private sealed class StubApiClient(ResolveExternalUserResult scriptedResult) : IApiClient
    {
        public ResolveExternalUserRequestDto? LastRequest { get; private set; }

        public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ApiHealthResponse?>(null);

        public Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
            string? searchText,
            bool displayPublished,
            bool displayDraft,
            bool displayScenarios,
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
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(scriptedResult);
        }
    }
}
