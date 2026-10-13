using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Shared URL builders for cross-links between the disease-profile pages (EditProfileQuestions,
/// Notes), so the route/page names are not duplicated across page models.
/// </summary>
public static class ProfileNavigationUrls
{
    /// <summary>Builds the URL to the standalone Notes page for a profile.</summary>
    public static string? BuildNotesUrl(this PageModel page, Guid profileId) =>
        page.Url.Page("/SurveillanceProfiles/Notes", new { profileId });

    /// <summary>Builds the URL to a section of the EditProfileQuestions page for a profile,
    /// optionally selecting a specific left-nav section.</summary>
    public static string? BuildEditProfileQuestionsUrl(this PageModel page, Guid profileId, Guid? sectionId = null) =>
        sectionId.HasValue
            ? page.Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId, section = sectionId.Value })
            : page.Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId });
}
