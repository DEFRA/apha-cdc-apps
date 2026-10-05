using System.Security.Claims;
using CDC.Web.Authorization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Authorization;

/// <summary>End-to-end tests wiring the real policies (<see cref="AuthorizationDependencyInjection"/>)
/// so the role-to-policy mapping itself is verified, not just the handler in isolation.</summary>
public class NavigationAuthorizationServiceTests
{
    [Fact]
    public async Task ProfileEditor_CanAccessEveryHeaderLink()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.ProfileEditor);

        Assert.True(await service.CanViewProfileSearchAsync(user));
        Assert.True(await service.CanCreateProfileAsync(user));
        Assert.True(await service.CanViewProfileComparisonAsync(user));
        Assert.True(await service.CanViewReportsAsync(user));
        Assert.True(await service.CanManageSpeciesDataAsync(user));
        Assert.True(await service.CanEditUserGuidanceAsync(user));
        Assert.True(await service.CanViewSpeciesDataAsync(user));
        Assert.True(await service.CanViewReviewTimingAsync(user));
    }

    [Fact]
    public async Task PolicyProfileUser_CanOnlyAccessProfileSearchAndReports()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.PolicyProfileUser);

        Assert.True(await service.CanViewProfileSearchAsync(user));
        Assert.True(await service.CanViewReportsAsync(user));

        Assert.False(await service.CanCreateProfileAsync(user));
        Assert.False(await service.CanViewProfileComparisonAsync(user));
        Assert.False(await service.CanManageSpeciesDataAsync(user));
        Assert.False(await service.CanEditUserGuidanceAsync(user));
        Assert.False(await service.CanViewSpeciesDataAsync(user));
        Assert.False(await service.CanViewReviewTimingAsync(user));
    }

    [Fact]
    public async Task Contributor_CanOnlyAccessProfileSearchAndReports()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.Contributor);

        Assert.True(await service.CanViewProfileSearchAsync(user));
        Assert.True(await service.CanViewReportsAsync(user));

        Assert.False(await service.CanCreateProfileAsync(user));
        Assert.False(await service.CanViewProfileComparisonAsync(user));
        Assert.False(await service.CanManageSpeciesDataAsync(user));
        Assert.False(await service.CanEditUserGuidanceAsync(user));
        Assert.False(await service.CanViewSpeciesDataAsync(user));
        Assert.False(await service.CanViewReviewTimingAsync(user));
    }

    [Fact]
    public async Task Reviewer_CanAccessProfileSearchComparisonReportsAndReviewTiming()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.Reviewer);

        Assert.True(await service.CanViewProfileSearchAsync(user));
        Assert.True(await service.CanViewProfileComparisonAsync(user));
        Assert.True(await service.CanViewReportsAsync(user));
        Assert.True(await service.CanViewReviewTimingAsync(user));

        Assert.False(await service.CanCreateProfileAsync(user));
        Assert.False(await service.CanManageSpeciesDataAsync(user));
        Assert.False(await service.CanEditUserGuidanceAsync(user));
        Assert.False(await service.CanViewSpeciesDataAsync(user));
    }

    [Fact]
    public async Task UserManagementSystem_IsDeniedEveryHeaderLink()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.UserManagementSystem);

        Assert.False(await service.CanViewProfileSearchAsync(user));
        Assert.False(await service.CanCreateProfileAsync(user));
        Assert.False(await service.CanViewProfileComparisonAsync(user));
        Assert.False(await service.CanViewReportsAsync(user));
        Assert.False(await service.CanManageSpeciesDataAsync(user));
        Assert.False(await service.CanEditUserGuidanceAsync(user));
        Assert.False(await service.CanViewSpeciesDataAsync(user));
        Assert.False(await service.CanViewReviewTimingAsync(user));
    }

    [Fact]
    public async Task UnauthenticatedUser_IsDeniedEveryHeaderLink()
    {
        var service = CreateService();
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await service.CanViewProfileSearchAsync(user));
        Assert.False(await service.CanViewReportsAsync(user));
    }

    [Fact]
    public async Task CanAccessAsync_EvaluatesAnArbitraryPolicyName_ForDataDrivenNavigation()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.ProfileEditor);

        Assert.True(await service.CanAccessAsync(user, AuthorizationPolicies.CanCreateProfile));
    }

    private static INavigationAuthorizationService CreateService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCdcWebAuthorization(new ConfigurationBuilder().Build());

        return services.BuildServiceProvider().GetRequiredService<INavigationAuthorizationService>();
    }

    private static ClaimsPrincipal CreateUser(params string[] roles)
    {
        var identity = new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
