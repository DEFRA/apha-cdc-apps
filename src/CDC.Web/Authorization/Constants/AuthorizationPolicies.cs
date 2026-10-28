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

    /// <summary>Legacy: <c>Profile.CanEditProfile()</c>. Search results: the "Edit Title" link and the Edit profile title page.</summary>
    public const string CanEditProfile = nameof(CanEditProfile);

    /// <summary>Legacy: <c>GetProfileVersionComparisonReportCommand.CanGetReport()</c>. Header: "Compare
    /// profile versions". Profile editor only.</summary>
    public const string CanViewProfileComparison = nameof(CanViewProfileComparison);

    /// <summary>Legacy: <c>GetProfileRankingReportCommand.CanGetReport()</c>. The interactive Disease ranking report: profile
    /// editor only. The header link is open to everyone (<see cref="CanViewReports"/>); anyone else is sent to the stored
    /// "DiseaseRankingReport" static report, as legacy <c>SetupDiseaseRankingLink</c> did.</summary>
    public const string CanViewDiseaseRankingReport = nameof(CanViewDiseaseRankingReport);

    /// <summary>Legacy: <c>StaticReportList.CanGetList()</c>. Header: "General reports" and "Disease ranking report" (the
    /// stored report). Open to every signed-in user.</summary>
    public const string CanViewReports = nameof(CanViewReports);

    /// <summary>Footer: "Help using D2R2" and "D2R2 Quality Statement", and the document download behind them. Open to every
    /// signed-in user.</summary>
    public const string CanViewHelp = nameof(CanViewHelp);

    /// <summary>Legacy: <c>StaticReport.CanDelete</c> and <c>UploadStaticReportCommand.CanUploadStaticReport()</c>. General reports page: delete and add.</summary>
    public const string CanManageReports = nameof(CanManageReports);

    /// <summary>Legacy: <c>SpeciesDataChange.CanAddSpeciesDataChange()</c>. Header: "Maintain species data".</summary>
    public const string CanManageSpeciesData = nameof(CanManageSpeciesData);

    /// <summary>Legacy: <c>ProfileQuestion.CanEditObject()</c>. Header: "Questions and guidance reports".</summary>
    public const string CanEditUserGuidance = nameof(CanEditUserGuidance);

    /// <summary>Legacy: <c>SpeciesSectionList.CanGetSpeciesSectionList()</c>. Header: "View species data". Open to every
    /// signed-in user.</summary>
    public const string CanViewSpeciesData = nameof(CanViewSpeciesData);

    /// <summary>Legacy: <c>DiseaseReviewsFilterInfo.CanGetObject()</c>. Header: "Review Timings". Profile editor only.</summary>
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

    /// <summary>Legacy: <c>DiseaseFilterValues.CanGetList()</c>. The Edit disease ranking filters page. Profile editor only.</summary>
    public const string CanManageDiseaseRankingFilters = nameof(CanManageDiseaseRankingFilters);
}
