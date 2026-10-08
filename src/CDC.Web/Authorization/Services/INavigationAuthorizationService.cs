using System.Security.Claims;

namespace CDC.Web.Authorization.Services;

/// <summary>
/// Evaluates whether the current user may view each Header navigation link. Wraps
/// <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationService"/> - never duplicates the
/// allowed-roles logic defined by the policies in
/// <see cref="Policies.AuthorizationPolicyRegistration"/>.
/// </summary>
public interface INavigationAuthorizationService
{
    /// <summary>Evaluates an arbitrary named policy. Used by data-driven navigation (the Header
    /// menu's <see cref="CDC.Web.Models.HeaderMenuLink.RequiredPolicy"/>) so new links never need
    /// a bespoke method here.</summary>
    Task<bool> CanAccessAsync(ClaimsPrincipal user, string policyName);

    /// <summary>Legacy: <c>ProfileInfoList.CanGetProfileInfoList()</c>.</summary>
    Task<bool> CanViewProfileSearchAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>Profile.CanCreateProfile()</c>.</summary>
    Task<bool> CanCreateProfileAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>GetProfileRankingReportCommand.CanGetReport()</c>.</summary>
    Task<bool> CanViewProfileComparisonAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>StaticReportList.CanGetList()</c>.</summary>
    Task<bool> CanViewReportsAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>SpeciesDataChange.CanAddSpeciesDataChange()</c>.</summary>
    Task<bool> CanManageSpeciesDataAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>ProfileQuestion.CanEditObject()</c>.</summary>
    Task<bool> CanEditUserGuidanceAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>SpeciesSectionList.CanGetSpeciesSectionList()</c>.</summary>
    Task<bool> CanViewSpeciesDataAsync(ClaimsPrincipal user);

    /// <summary>Legacy: <c>DiseaseReviewsFilterInfo.CanGetObject()</c>.</summary>
    Task<bool> CanViewReviewTimingAsync(ClaimsPrincipal user);
}
