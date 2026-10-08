using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Authorization.Policies;

/// <summary>
/// Registers every named policy from <see cref="AuthorizationPolicies"/> against an
/// <see cref="AllowedRolesRequirement"/>, with the allowed roles for each policy centralized
/// here - the single place to change a role mapping. See the Header implementation summary for
/// how each policy's role list maps back to its legacy <c>CanXxx()</c> equivalent, including
/// three policies where the allowed roles were deliberately widened/narrowed from the legacy
/// behaviour at the requester's explicit instruction.
/// </summary>
public static class AuthorizationPolicyRegistration
{
    /// <summary>Adds every policy used by the application to <paramref name="options"/>.</summary>
    public static void AddCdcWebPolicies(this AuthorizationOptions options)
    {
        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewProfileSearch,
            AuthorizationRoles.ProfileEditor, AuthorizationRoles.PolicyProfileUser,
            AuthorizationRoles.Contributor, AuthorizationRoles.Reviewer);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanCreateProfile,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewProfileComparison,
            AuthorizationRoles.ProfileEditor, AuthorizationRoles.Reviewer);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewReports,
            AuthorizationRoles.ProfileEditor, AuthorizationRoles.PolicyProfileUser,
            AuthorizationRoles.Contributor, AuthorizationRoles.Reviewer);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanManageSpeciesData,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanEditUserGuidance,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewSpeciesData,
            AuthorizationRoles.ProfileEditor);

        AddAllowedRolesPolicy(options, AuthorizationPolicies.CanViewReviewTiming,
            AuthorizationRoles.ProfileEditor, AuthorizationRoles.Reviewer);

        // Scaffolded for the footer/admin-pages phase - not yet wired to any page or nav item.
        // Role lists are a provisional ProfileEditor-only default; confirm against the relevant
        // legacy CanXxx() method when each of these is actually implemented.
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
    }

    private static void AddAllowedRolesPolicy(AuthorizationOptions options, string policyName, params string[] allowedRoles) =>
        options.AddPolicy(policyName, policy => policy.Requirements.Add(new AllowedRolesRequirement(allowedRoles)));
}
