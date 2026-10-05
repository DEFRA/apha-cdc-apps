using System.Security.Claims;
using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Authorization.Services;

/// <summary>
/// Default <see cref="INavigationAuthorizationService"/>. Every named method is a one-line call
/// to <see cref="CanAccessAsync"/> with the matching policy constant - all authorization logic
/// itself lives in <see cref="AuthorizationPolicyRegistration"/>'s policies and
/// <see cref="Handlers.AllowedRolesAuthorizationHandler"/>, never duplicated here.
/// </summary>
/// <param name="authorizationService">The ASP.NET Core authorization service.</param>
public sealed class NavigationAuthorizationService(IAuthorizationService authorizationService) : INavigationAuthorizationService
{
    /// <inheritdoc />
    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, string policyName)
    {
        var result = await authorizationService.AuthorizeAsync(user, policyName);
        return result.Succeeded;
    }

    /// <inheritdoc />
    public Task<bool> CanViewProfileSearchAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanViewProfileSearch);

    /// <inheritdoc />
    public Task<bool> CanCreateProfileAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanCreateProfile);

    /// <inheritdoc />
    public Task<bool> CanViewProfileComparisonAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanViewProfileComparison);

    /// <inheritdoc />
    public Task<bool> CanViewReportsAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanViewReports);

    /// <inheritdoc />
    public Task<bool> CanManageSpeciesDataAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanManageSpeciesData);

    /// <inheritdoc />
    public Task<bool> CanEditUserGuidanceAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanEditUserGuidance);

    /// <inheritdoc />
    public Task<bool> CanViewSpeciesDataAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanViewSpeciesData);

    /// <inheritdoc />
    public Task<bool> CanViewReviewTimingAsync(ClaimsPrincipal user) =>
        CanAccessAsync(user, AuthorizationPolicies.CanViewReviewTiming);
}
