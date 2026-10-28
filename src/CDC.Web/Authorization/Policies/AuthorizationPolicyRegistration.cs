using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Authorization.Policies;

/// <summary>
/// Registers every named policy from <see cref="AuthorizationPolicies"/>, with the allowed roles for each
/// role-based policy (an <see cref="AllowedRolesRequirement"/>) centralized here - the single place to change a
/// role mapping. Each list follows the legacy <c>CanXxx()</c> method it replaces. Searching disease profiles, viewing species
/// data, the general reports and the Help and Support footer links are open to every signed-in user, as in legacy. What a
/// contributor or reviewer may do with a particular profile is decided by CDC.Api, from their role on that profile.
/// </summary>
public static class AuthorizationPolicyRegistration
{
    /// <summary>Adds every policy used by the application to <paramref name="options"/>.</summary>
    public static void AddCdcWebPolicies(this AuthorizationOptions options)
    {
        AddSignedInUserPolicy(options, AuthorizationPolicies.CanViewProfileSearch);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanCreateProfile,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanEditProfile,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewProfileComparison,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewDiseaseRankingReport,
            AuthorizationRoles.ProfileEditor);

        AddSignedInUserPolicy(options, AuthorizationPolicies.CanViewReports);

        AddSignedInUserPolicy(options, AuthorizationPolicies.CanViewHelp);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageReports,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageSpeciesData,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanEditUserGuidance,
            AuthorizationRoles.ProfileEditor);

        AddSignedInUserPolicy(options, AuthorizationPolicies.CanViewSpeciesData);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewReviewTiming,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewContributionsReport,
            AuthorizationRoles.ProfileEditor, AuthorizationRoles.PolicyProfileUser);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewBespokeReport,
            AuthorizationRoles.ProfileEditor, AuthorizationRoles.PolicyProfileUser);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.IsProfileEditor,
            AuthorizationRoles.ProfileEditor);

        // Footer admin links (User Admin and Cross Profile Admin), which the header Menu does not carry. Role
        // lists are a ProfileEditor-only default; confirm against each legacy CanXxx() method named on the policy.
        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageGlobalUsers,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageReferenceData,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageExternalUsers,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManagePrioritisationVariables,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageCrossCuttingScores,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageDiseaseRankingFilters,
            AuthorizationRoles.ProfileEditor);
    }

    private static void AddAllowedRolesPolicy(AuthorizationOptions options, string policyName, params string[] allowedRoles) =>
        options.AddPolicy(policyName, policy => policy.Requirements.Add(new AllowedRolesRequirement(allowedRoles)));

    // Any signed-in user, internal or external, whatever their roles, except the UserManagementSystem service role, which every policy denies.
    private static void AddSignedInUserPolicy(AuthorizationOptions options, string policyName) =>
        options.AddPolicy(policyName, policy => policy
            .AddAuthenticationSchemes(InternalOrExternalAuthenticationScheme.Name)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => !context.User.IsInRole(AuthorizationRoles.UserManagementSystem)));
}
