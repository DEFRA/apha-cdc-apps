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
    /// <summary>Legacy: <c>ProfileInfoList.CanGetProfileInfoList()</c>. Header: "Search disease profiles".</summary>
    public const string CanViewProfileSearch = nameof(CanViewProfileSearch);

    /// <summary>Legacy: <c>Profile.CanCreateProfile()</c>. Header: "Create profile".</summary>
    public const string CanCreateProfile = nameof(CanCreateProfile);

    /// <summary>Legacy: <c>GetProfileRankingReportCommand.CanGetReport()</c>. Header: "Compare
    /// profile versions" and "Disease ranking report" (legacy drives both links' visibility from
    /// this one check - see <c>NavigationLinks.ascx.vb</c>).</summary>
    public const string CanViewProfileComparison = nameof(CanViewProfileComparison);

    /// <summary>Legacy: <c>StaticReportList.CanGetList()</c>. Header: "General reports".</summary>
    public const string CanViewReports = nameof(CanViewReports);

    /// <summary>Legacy: <c>SpeciesDataChange.CanAddSpeciesDataChange()</c>. Header: "Maintain species data".</summary>
    public const string CanManageSpeciesData = nameof(CanManageSpeciesData);

    /// <summary>Legacy: <c>ProfileQuestion.CanEditObject()</c>. Header: "Questions and guidance reports".</summary>
    public const string CanEditUserGuidance = nameof(CanEditUserGuidance);

    /// <summary>Legacy: <c>SpeciesSectionList.CanGetSpeciesSectionList()</c>. Header: "View species data".</summary>
    public const string CanViewSpeciesData = nameof(CanViewSpeciesData);

    /// <summary>Legacy: <c>DiseaseReviewsFilterInfo.CanGetObject()</c>. Header: "Review Timings".</summary>
    public const string CanViewReviewTiming = nameof(CanViewReviewTiming);

    /// <summary>Legacy: <c>GlobalUserList.CanGetGlobalUserList()</c>. Not yet wired to any page -
    /// scaffolded for the footer/admin-pages phase.</summary>
    public const string CanManageGlobalUsers = nameof(CanManageGlobalUsers);

    /// <summary>Legacy: <c>ReferenceTableInfoList.CanGetList()</c>. Not yet wired to any page -
    /// scaffolded for the footer/admin-pages phase.</summary>
    public const string CanManageReferenceData = nameof(CanManageReferenceData);

    /// <summary>Not yet wired to any page - scaffolded for the footer/admin-pages phase.</summary>
    public const string CanManageExternalUsers = nameof(CanManageExternalUsers);

    /// <summary>Legacy: <c>ProfilePrioritisationMetadata.CanGetObject()</c>. Not yet wired to any
    /// page - scaffolded for the footer/admin-pages phase.</summary>
    public const string CanManagePrioritisationVariables = nameof(CanManagePrioritisationVariables);

    /// <summary>Legacy: <c>SpeciesPrioritisationMetadata.CanGetObject()</c>. Not yet wired to any
    /// page - scaffolded for the footer/admin-pages phase.</summary>
    public const string CanManageCrossCuttingScores = nameof(CanManageCrossCuttingScores);
}
