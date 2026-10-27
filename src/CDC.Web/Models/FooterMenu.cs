using CDC.Web.Authorization.Constants;

namespace CDC.Web.Models;

/// <summary>A footer link. <paramref name="RequiredPolicy"/> is one of <see cref="AuthorizationPolicies"/>,
/// evaluated by <c>INavigationAuthorizationService</c> before the link is rendered - see
/// <c>_FooterNavigation.cshtml</c>.</summary>
public sealed record FooterMenuLink(string Text, string PagePath, string RequiredPolicy, object? RouteValues = null);

public sealed record FooterMenuSection(string Title, IReadOnlyList<FooterMenuLink> Links);

/// Static definition of the footer navigation, as the legacy <c>Footer.ascx</c> lists it: the User Admin
/// and Cross Profile Admin links live here only, never in the header Menu.
public static class FooterMenu
{
    public static readonly IReadOnlyList<FooterMenuSection> Sections =
    [
        new("User Admin",
        [
            new("Global users", "/UserAdmin/GlobalUsers", AuthorizationPolicies.CanManageGlobalUsers),
            new("External users", "/UserAdmin/ExternalUsers", AuthorizationPolicies.CanManageExternalUsers),
        ]),
        new("Cross Profile Admin",
        [
            new("Maintain reference data", "/CrossProfileAdmin/ReferenceData", AuthorizationPolicies.CanManageReferenceData),
            new("Maintain prioritisation variables", "/CrossProfileAdmin/PrioritisationVariables", AuthorizationPolicies.CanManagePrioritisationVariables),
            new("Maintain cross-cutting issue scores", "/CrossProfileAdmin/CrossCuttingIssueScores", AuthorizationPolicies.CanManageCrossCuttingScores),
        ]),
        new("Help and Support",
        [
            new("Help using D2R2", "/HelpSupport/StaticReports", AuthorizationPolicies.CanViewHelp, new { UserManual = 1 }),
            new("D2R2 Quality Statement", "/HelpSupport/QualityStatement", AuthorizationPolicies.CanViewHelp),
        ]),
    ];
}
