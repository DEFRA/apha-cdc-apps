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
        Assert.True(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewDiseaseRankingReport));
        Assert.True(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewHelp));
        Assert.True(await service.CanAccessAsync(user, AuthorizationPolicies.CanManageDiseaseRankingFilters));
    }

    [Fact]
    public async Task PolicyProfileUser_CanSearchRunReportsAndViewSpeciesDataButNotCompareVersionsOrTheDiseaseRanking()
    {
        var service = CreateService();
        var user = CreateUser(AuthorizationRoles.PolicyProfileUser);

        Assert.True(await service.CanViewProfileSearchAsync(user));
        Assert.True(await service.CanViewReportsAsync(user));
        Assert.True(await service.CanViewSpeciesDataAsync(user));
        Assert.True(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewHelp));

        Assert.False(await service.CanViewProfileComparisonAsync(user));
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewDiseaseRankingReport));
        Assert.False(await service.CanCreateProfileAsync(user));
        Assert.False(await service.CanManageSpeciesDataAsync(user));
        Assert.False(await service.CanEditUserGuidanceAsync(user));
        Assert.False(await service.CanViewReviewTimingAsync(user));
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanManageDiseaseRankingFilters));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SomeCidmRole")]
    public async Task SignedInUserWithNoGlobalRole_CanAccessOnlySearchReportsHelpAndViewSpeciesData(string? role)
    {
        // No role models an internal user with limited access; an unnamed role models an external user whose CIDM role is not one of
        // AuthorizationRoles. An external user holds no global role: their rights are per profile, and CDC.Api applies them.
        var service = CreateService();
        var user = role is null ? CreateUser() : CreateUser(role);

        Assert.True(await service.CanViewProfileSearchAsync(user));
        Assert.True(await service.CanViewSpeciesDataAsync(user));
        Assert.True(await service.CanViewReportsAsync(user));
        Assert.True(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewHelp));

        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewDiseaseRankingReport));
        Assert.False(await service.CanCreateProfileAsync(user));
        Assert.False(await service.CanViewProfileComparisonAsync(user));
        Assert.False(await service.CanManageSpeciesDataAsync(user));
        Assert.False(await service.CanEditUserGuidanceAsync(user));
        Assert.False(await service.CanViewReviewTimingAsync(user));
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanManageDiseaseRankingFilters));
    }

    [Theory]
    [InlineData("Contributor")]
    [InlineData("Reviewer")]
    public async Task ARoleNamedAfterAProfileRole_GrantsNothingGlobal(string role)
    {
        // The technical, policy and VA authors and reviewers are profile roles held in the database; naming one in a claim opens nothing.
        var service = CreateService();
        var user = CreateUser(role);

        Assert.False(await service.CanViewProfileComparisonAsync(user));
        Assert.False(await service.CanViewReviewTimingAsync(user));
        Assert.False(await service.CanViewContributionsReportAsync(user));
        Assert.False(await service.CanViewBespokeReportAsync(user));
        Assert.False(await service.CanEditProfileAsync(user));
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
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewDiseaseRankingReport));
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewHelp));
    }

    [Fact]
    public async Task UnauthenticatedUser_IsDeniedEveryHeaderLink()
    {
        var service = CreateService();
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await service.CanViewProfileSearchAsync(user));
        Assert.False(await service.CanViewReportsAsync(user));
        Assert.False(await service.CanViewSpeciesDataAsync(user));
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewDiseaseRankingReport));
        Assert.False(await service.CanAccessAsync(user, AuthorizationPolicies.CanViewHelp));
    }

    [Fact]
    public async Task CanViewContributionsReport_IsAllowedForProfileEditorAndPolicyProfileUserOnly()
    {
        var service = CreateService();

        Assert.True(await service.CanViewContributionsReportAsync(CreateUser(AuthorizationRoles.ProfileEditor)));
        Assert.True(await service.CanViewContributionsReportAsync(CreateUser(AuthorizationRoles.PolicyProfileUser)));
        Assert.False(await service.CanViewContributionsReportAsync(CreateUser()));
        Assert.False(await service.CanViewContributionsReportAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task CanViewBespokeReport_AndIsProfileEditor_FollowTheirLegacyRoles()
    {
        var service = CreateService();

        Assert.True(await service.CanViewBespokeReportAsync(CreateUser(AuthorizationRoles.ProfileEditor)));
        Assert.True(await service.CanViewBespokeReportAsync(CreateUser(AuthorizationRoles.PolicyProfileUser)));
        Assert.False(await service.CanViewBespokeReportAsync(CreateUser()));
        Assert.False(await service.CanViewBespokeReportAsync(CreateUser(AuthorizationRoles.UserManagementSystem)));

        Assert.True(await service.IsProfileEditorAsync(CreateUser(AuthorizationRoles.ProfileEditor)));
        Assert.False(await service.IsProfileEditorAsync(CreateUser(AuthorizationRoles.PolicyProfileUser)));
        Assert.False(await service.IsProfileEditorAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    // Legacy Profile.CanEditProfile(): a profile editor who is not a user management system account.
    [Fact]
    public async Task CanEditProfile_IsAllowedForProfileEditorsOnly()
    {
        var service = CreateService();

        Assert.True(await service.CanEditProfileAsync(CreateUser(AuthorizationRoles.ProfileEditor)));
        Assert.False(await service.CanEditProfileAsync(CreateUser(AuthorizationRoles.PolicyProfileUser)));
        Assert.False(await service.CanEditProfileAsync(CreateUser()));
        Assert.False(await service.CanEditProfileAsync(CreateUser(AuthorizationRoles.ProfileEditor, AuthorizationRoles.UserManagementSystem)));
        Assert.False(await service.CanEditProfileAsync(new ClaimsPrincipal(new ClaimsIdentity())));
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
        services.AddCdcWebAuthorization();

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
