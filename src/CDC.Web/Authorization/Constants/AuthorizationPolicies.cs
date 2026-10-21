namespace CDC.Web.Authorization.Constants;

/// <summary>
/// Named authorization policies, replacing the legacy CSLA business-object
/// <c>CanXxx()</c>/<c>CanGetXxx()</c> static authorization methods with centralized,
/// ASP.NET Core policy-based checks. See <see cref="Policies.AuthorizationPolicyRegistration"/>
/// for which roles each policy allows, and the Header implementation summary for how each policy
/// maps back to its legacy equivalent.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Legacy: <c>ProfileInfoList.CanGetProfileInfoList()</c>. Header: "Search disease profiles". Open to every
    /// signed-in user.</summary>
    public const string CanViewProfileSearch = nameof(CanViewProfileSearch);

    /// <summary>Legacy: <c>Profile.CanCreateProfile()</c>. Header: "Create profile".</summary>
    public const string CanCreateProfile = nameof(CanCreateProfile);

    /// <summary>Legacy: <c>GetProfileRankingReportCommand.CanGetReport()</c>. Header: "Compare
    /// profile versions".</summary>
    public const string CanViewProfileComparison = nameof(CanViewProfileComparison);

    /// <summary>Legacy: <c>NavigationLinks.ascx.vb</c> shows the "Disease ranking report" link to every
    /// user (editors open the interactive report, everyone else the pre-generated static one); the page
    /// here is the same for every user. Header: "Disease ranking report". Open to every signed-in user.</summary>
    public const string CanViewDiseaseRankingReport = nameof(CanViewDiseaseRankingReport);

    /// <summary>Legacy: <c>StaticReportList.CanGetList()</c>. Header: "General reports". Footer: "Help using D2R2" and
    /// "D2R2 Quality Statement". Open to every signed-in user.</summary>
    public const string CanViewReports = nameof(CanViewReports);

    /// <summary>Legacy: <c>StaticReport.CanDelete</c> and <c>UploadStaticReportCommand.CanUploadStaticReport()</c>. General reports page: delete and add.</summary>
    public const string CanManageReports = nameof(CanManageReports);

    /// <summary>Legacy: <c>SpeciesDataChange.CanAddSpeciesDataChange()</c>. Header: "Maintain species data".</summary>
    public const string CanManageSpeciesData = nameof(CanManageSpeciesData);

    /// <summary>Legacy: <c>ProfileQuestion.CanEditObject()</c>. Header: "Questions and guidance reports".</summary>
    public const string CanEditUserGuidance = nameof(CanEditUserGuidance);

    /// <summary>Legacy: <c>SpeciesSectionList.CanGetSpeciesSectionList()</c>. Header: "View species data". Open to every
    /// signed-in user.</summary>
    public const string CanViewSpeciesData = nameof(CanViewSpeciesData);

    /// <summary>Legacy: <c>DiseaseReviewsFilterInfo.CanGetObject()</c>. Header: "Review Timings".</summary>
    public const string CanViewReviewTiming = nameof(CanViewReviewTiming);

    /// <summary>Legacy: <c>GetContributionsReportCommand.CanGetReport()</c>. Search results and Manage profile: the "Contributions" report link.</summary>
    public const string CanViewContributionsReport = nameof(CanViewContributionsReport);

    /// <summary>Legacy: <c>TemplateService.CanView()</c> / <c>GetProfileVersionBespokeReportCommand.CanGetReport()</c>. Profile reports: the "Create bespoke report" button and the bespoke report page.</summary>
    public const string CanViewBespokeReport = nameof(CanViewBespokeReport);

    /// <summary>Legacy: <c>ProfilesIdentity.IsProfileEditor</c> (also <c>TemplateService.CanEdit()</c>). Profile reports: hides the intro text; bespoke report: saving and deleting templates.</summary>
    public const string IsProfileEditor = nameof(IsProfileEditor);

    /// <summary>Legacy: <c>GlobalUserList.CanGetGlobalUserList()</c>. Footer: "Global users".</summary>
    public const string CanManageGlobalUsers = nameof(CanManageGlobalUsers);

    /// <summary>Legacy: <c>ReferenceTableInfoList.CanGetList()</c>. Footer: "Maintain reference data".</summary>
    public const string CanManageReferenceData = nameof(CanManageReferenceData);

    /// <summary>Legacy: <c>SsoUserMaintenance.CanGetSsoUserMaintenance()</c>. Footer: "External users".</summary>
    public const string CanManageExternalUsers = nameof(CanManageExternalUsers);

    /// <summary>Legacy: <c>ProfilePrioritisationMetadata.CanGetObject()</c>. Footer: "Maintain prioritisation
    /// variables".</summary>
    public const string CanManagePrioritisationVariables = nameof(CanManagePrioritisationVariables);

    /// <summary>Legacy: <c>SpeciesPrioritisationMetadata.CanGetObject()</c>. Footer: "Maintain cross-cutting issue
    /// scores".</summary>
    public const string CanManageCrossCuttingScores = nameof(CanManageCrossCuttingScores);
}
