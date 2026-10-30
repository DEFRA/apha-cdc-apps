using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
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

    /// <summary>Builds the URL that downloads the print version of a profile's current draft: the whole profile, or one of its sections.</summary>
    public static string? BuildPrintVersionUrl(this PageModel page, Guid profileId, Guid? sectionId = null) =>
        page.Url.Page(ViewProfilePrintVersionModel.PagePath, new { profileId, section = sectionId });

    /// <summary>Builds the URL that downloads one of the summary reports of a profile's current draft.</summary>
    public static string? BuildSummaryReportUrl(this PageModel page, Guid profileId, DraftSummaryReport report) =>
        page.Url.Page(ViewDraftSummaryReportModel.PagePath, new { profileId, report });

    /// <summary>Builds the URL to a section of the EditProfileQuestions page for a profile,
    /// optionally selecting a specific left-nav section.</summary>
    public static string? BuildEditProfileQuestionsUrl(this PageModel page, Guid profileId, Guid? sectionId = null) =>
        sectionId.HasValue
            ? page.Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId, section = sectionId.Value })
            : page.Url.Page("/SurveillanceProfiles/EditProfileQuestions", new { profileId });
}
