using CDC.Web.Authorization.Constants;

namespace CDC.Web.Models;

/// <summary>A header menu link. <paramref name="RequiredPolicy"/> is one of
/// <see cref="AuthorizationPolicies"/>, evaluated by <c>INavigationAuthorizationService</c>
/// before the link is rendered - see <c>_HeaderMenu.cshtml</c>.</summary>
public sealed record HeaderMenuLink(string Text, string PagePath, string RequiredPolicy);

public sealed record HeaderMenuSection(string Title, IReadOnlyList<HeaderMenuLink> Links);

/// Static definition of the header "Menu" navigation panel sections and links.
public static class HeaderMenu
{
    public static readonly IReadOnlyList<HeaderMenuSection> Sections =
    [
        new("Disease Profiles",
        [
            new("Search disease profiles", "/SurveillanceProfiles/Search", AuthorizationPolicies.CanViewProfileSearch),
            new("Create profile", "/DiseaseProfiles/Create", AuthorizationPolicies.CanCreateProfile),
            new("Compare profile versions", "/DiseaseProfiles/CompareVersions", AuthorizationPolicies.CanViewProfileComparison),
            new("Review Timings", "/DiseaseProfiles/ReviewTimings", AuthorizationPolicies.CanViewReviewTiming),
        ]),
        new("Reports",
        [
            new("General reports", "/Reports/General", AuthorizationPolicies.CanViewReports),
            new("Questions and guidance reports", "/Reports/QuestionsGuidance", AuthorizationPolicies.CanEditUserGuidance),
            new("Disease ranking report", "/Reports/DiseaseRanking", AuthorizationPolicies.CanViewProfileComparison),
        ]),
        new("Species Data",
        [
            new("View species data", "/ViewSpeciesData", AuthorizationPolicies.CanViewSpeciesData),
            new("Maintain species data", "/SpeciesData/Maintain", AuthorizationPolicies.CanManageSpeciesData),
        ]),
    ];
}

