using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Shared by every tab of a profile section (Questions, References, Further information, Review comments,
/// All Revision dates, Contributions), each its own Razor Page as in the legacy <c>EditProfile*.aspx</c> pages.
/// Loads what the legacy <c>NestedProfileQuestions</c> master page provided to all of them: the profile title and
/// version, the permission flags, the section list with its validation ticks, and the tab strip.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public abstract class ProfileSectionTabPageModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger logger) : PageModel
{
    /// <summary>The date format used by the Contributions and Review comments tables.</summary>
    protected const string DateDisplayFormat = "dd/MM/yyyy";

    /// <summary>Gets or sets the profile being browsed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the selected section id, bound from the querystring. Defaults to the first section.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Section { get; set; }

    /// <summary>Gets the profile's full display title (with its scenario title in brackets, for a "what-if"
    /// scenario), once loaded. Matches the legacy <c>ProfileVersionInfo.FullTitle</c>.</summary>
    public string? ProfileTitle { get; private set; }

    /// <summary>Gets the current version's display summary (for example, "Version 13.0 (Published) Valid from
    /// 11/09/2026"), shown beneath the title. <see langword="null"/> when the profile has no current version.</summary>
    public string? VersionInfo { get; private set; }

    /// <summary>Gets a value indicating whether the "Print full profile" link is shown (legacy <c>CanGetPrintVersion()</c>).</summary>
    public bool CanPrintProfileSection { get; private set; }

    /// <summary>Gets a value indicating whether the summary report links are shown (legacy <c>CanGetSummaryReport()</c>).</summary>
    public bool CanViewProfileReports { get; private set; }

    /// <summary>Gets a value indicating whether the section's answers and revision dates are editable (legacy
    /// <c>ProfileVersionSection.CanEditProfileVersionSection()</c>).</summary>
    public bool CanEditProfileSection { get; private set; }

    /// <summary>Gets a value indicating whether the "Clear" button is shown (legacy <c>CanClearProfileVersionSection()</c>).</summary>
    public bool CanClearProfileSection { get; private set; }

    /// <summary>Gets a value indicating whether the profile is a "what-if" scenario rather than the current situation, which
    /// decides which fields are read-only.</summary>
    public bool IsWhatIfScenario { get; private set; }

    /// <summary>Gets a value indicating whether the "Review comments" tab is shown (legacy <c>CanGetReviewCommentList()</c>).</summary>
    public bool CanViewReviewComments { get; private set; }

    /// <summary>Gets a value indicating whether the "Contributions" tab is shown (legacy <c>CanGetProfileContributionInfoList()</c>).</summary>
    public bool CanViewContributions { get; private set; }

    /// <summary>Gets a value indicating whether the "All Revision dates" tab is shown (legacy <c>CanGetRevisionInformation()</c>).</summary>
    public bool CanViewAllRevisionDates { get; private set; }

    /// <summary>Gets the profile's current version id, posted back by forms. <see cref="Guid.Empty"/> when the
    /// profile has no current version.</summary>
    public Guid CurrentProfileVersionId { get; private set; }

    /// <summary>Gets a value indicating whether the profile failed to load.</summary>
    public bool HasError { get; protected set; }

    /// <summary>Gets a value indicating whether the profile has no version to browse.</summary>
    public bool HasNoVersion { get; private set; }

    /// <summary>Gets the profile reference sections, in section-number order, read from CDC.Api (never hardcoded).</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> Sections { get; private set; } = [];

    /// <summary>Gets the selected section.</summary>
    public ProfileSectionMetadataDto? CurrentSection { get; private set; }

    /// <summary>Gets the section before <see cref="CurrentSection"/>, if any.</summary>
    public ProfileSectionMetadataDto? PreviousSection { get; private set; }

    /// <summary>Gets the section after <see cref="CurrentSection"/>, if any.</summary>
    public ProfileSectionMetadataDto? NextSection { get; private set; }

    /// <summary>Gets each section's validation status (section id to whether it passed validation), used to render
    /// the green tick/red cross against each left-nav entry. Sections absent from it show no icon.</summary>
    public IReadOnlyDictionary<Guid, bool> SectionValidationStatus { get; private set; } = new Dictionary<Guid, bool>();

    private HashSet<Guid> invalidQuestionIds = [];

    private HashSet<Guid> irrelevantQuestionIds = [];

    /// <summary>Gets the tab this page implements, used to highlight the active tab and to build pagination links.</summary>
    public abstract ProfileSectionTab ActiveTab { get; }

    /// <summary>Gets a value indicating whether the profile, a version and a section all loaded.</summary>
    public bool IsReady => !HasError && !HasNoVersion && CurrentSection is not null;

    /// <summary>Gets a value indicating whether the page is reporting a validation or save error, which puts "Error: " at
    /// the start of the page title as the GOV.UK Design System requires. A page that has error messages overrides this.</summary>
    public virtual bool HasFormErrors => false;

    /// <summary>Gets the page of a paginated list this page is showing, which goes in the page title as the GOV.UK Design System
    /// asks. A page that paginates a list overrides this; otherwise it is 1.</summary>
    public virtual int ListPageNumber => 1;

    /// <summary>Gets how many pages the list this page shows has. A page that paginates a list overrides this; otherwise it is 1.</summary>
    public virtual int ListTotalPages => 1;

    /// <summary>Gets the typed client for the profile sections endpoints.</summary>
    protected IProfileSectionsApiService ProfileSectionsApi => profileSectionsApiService;

    /// <summary>Gets the logger.</summary>
    protected ILogger Logger => logger;

    /// <summary>Gets one section's validation status for the left-nav tick/cross icon, or <see langword="null"/>
    /// when it is not known (for example, after a load failure).</summary>
    /// <param name="sectionId">The section.</param>
    /// <returns>Whether the section is valid, or <see langword="null"/> when unknown.</returns>
    public bool? GetSectionValidationStatus(Guid sectionId) =>
        SectionValidationStatus.TryGetValue(sectionId, out var isValid) ? isValid : null;

    /// <summary>Gets whether one question of the selected section passed validation, for the green tick/red cross beside
    /// the question, or <see langword="null"/> when the section's validation status is not known.</summary>
    /// <param name="questionId">The question.</param>
    /// <returns>Whether the question has no validation errors, or <see langword="null"/> when unknown.</returns>
    public bool? GetQuestionValidationStatus(Guid questionId) =>
        CurrentSection is { } section && GetSectionValidationStatus(section.Id) is not null ? !invalidQuestionIds.Contains(questionId) : null;

    /// <summary>Gets whether the answers make one question of the selected section irrelevant, so the page hides it as the
    /// legacy <c>HideAndClearIrrelevantQuestion</c> did. A question is shown when this is not known.</summary>
    /// <param name="questionId">The question.</param>
    /// <returns><see langword="true"/> when the question is irrelevant.</returns>
    public bool IsQuestionIrrelevant(Guid questionId) => irrelevantQuestionIds.Contains(questionId);

    /// <summary>Builds the URL a left-nav section link opens. Legacy <c>ProfileSectionLinks</c> always opens the
    /// section's Questions tab, whichever tab is showing.</summary>
    /// <param name="sectionId">The section.</param>
    /// <returns>The section's Questions page URL.</returns>
    public string? BuildSectionUrl(Guid sectionId) =>
        Url.Page(ProfileSectionTabs.PagePath(ProfileSectionTab.Questions), new { profileId = ProfileId, section = sectionId });

    /// <summary>Builds the URL a previous/next pagination link opens. Legacy <c>SectionPagination.TargetUrl</c> keeps
    /// the user on the tab they are on.</summary>
    /// <param name="sectionId">The section.</param>
    /// <returns>The same tab's page URL for that section.</returns>
    public string? BuildPaginationUrl(Guid sectionId) =>
        Url.Page(ProfileSectionTabs.PagePath(ActiveTab), new { profileId = ProfileId, section = sectionId });

    /// <summary>Builds the URL that downloads the print version of the profile, which the legacy page opened with
    /// <c>window.open('ShowProfilePrintVersion.aspx?...')</c>.</summary>
    /// <param name="sectionId">The one section to print, or <see langword="null"/> to print the full profile.</param>
    /// <returns>The print page URL.</returns>
    public string? BuildPrintUrl(Guid? sectionId = null) =>
        Url.Page(ViewProfilePrintVersionModel.PagePath, new { profileId = ProfileId, section = sectionId });

    /// <summary>Builds the tab strip for the selected section.</summary>
    /// <param name="submitHandler">When set, tabs post to this page handler (which receives the tab's name in
    /// <c>tab</c>) instead of being plain links, so the page can save before navigating.</param>
    /// <returns>The visible tabs with the active one marked.</returns>
    public ProfileSectionTabsViewModel BuildTabs(string? submitHandler = null)
    {
        var section = CurrentSection ?? throw new InvalidOperationException("The tab strip needs a selected section.");

        var tabs = ProfileSectionTabs.VisibleTabs(CanViewReviewComments, CanViewAllRevisionDates, CanViewContributions)
            .Select(tab => new ProfileSectionTabItem(
                tab,
                tab == ProfileSectionTab.Questions ? ProfileSectionTabs.FormatShortName(section.ShortName) : ProfileSectionTabs.Text(tab),
                Url.Page(ProfileSectionTabs.PagePath(tab), new { profileId = ProfileId, section = section.Id }),
                tab == ActiveTab))
            .ToList();

        var submitUrl = submitHandler is null
            ? null
            : Url.Page(ProfileSectionTabs.PagePath(ActiveTab), submitHandler, new { profileId = ProfileId, section = section.Id });

        return new ProfileSectionTabsViewModel(tabs, submitUrl);
    }

    /// <summary>
    /// Loads what every tab needs: the profile, its flags, the sections, the selected section and every section's
    /// validation status. Sets <see cref="HasError"/> when the profile cannot be loaded and <see cref="HasNoVersion"/>
    /// when it has no version; callers check <see cref="IsReady"/> before loading their own content.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see cref="NotFound()"/> when the profile does not exist; otherwise <see langword="null"/>.</returns>
    protected async Task<IActionResult?> LoadProfileContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (profile is null)
            {
                return NotFound();
            }

            ApplyProfileFlags(profile);

            var metadata = await profileSectionsApiService.GetProfileQuestionnaireMetadataAsync(cancellationToken);
            Sections = metadata.Sections;

            if (Sections.Count == 0)
            {
                return null;
            }

            CurrentSection = (Section.HasValue ? Sections.FirstOrDefault(s => s.Id == Section.Value) : null) ?? Sections[0];
            var currentIndex = Sections.ToList().IndexOf(CurrentSection);
            PreviousSection = currentIndex > 0 ? Sections[currentIndex - 1] : null;
            NextSection = currentIndex < Sections.Count - 1 ? Sections[currentIndex + 1] : null;

            if (profile.CurrentProfileVersionId == Guid.Empty)
            {
                HasNoVersion = true;
                return null;
            }

            CurrentProfileVersionId = profile.CurrentProfileVersionId;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfilePageLoadFailed(exception, ProfileId);
            HasError = true;
            return null;
        }

        await RefreshValidationStatusAsync(cancellationToken);

        return null;
    }

    private void ApplyProfileFlags(ManageProfileViewModel profile)
    {
        ProfileTitle = profile.FullTitle;
        VersionInfo = profile.CurrentVersionDisplay;
        CanPrintProfileSection = profile.CanPrintProfileSection;
        CanViewProfileReports = profile.CanViewProfileReports;
        CanEditProfileSection = profile.CanEditProfileSection;
        CanClearProfileSection = profile.CanClearProfileSection;
        IsWhatIfScenario = profile.IsWhatIfScenario;
        CanViewReviewComments = profile.CanViewReviewComments;
        CanViewContributions = profile.CanViewContributions;
        CanViewAllRevisionDates = profile.CanViewAllRevisionDates;
    }

    /// <summary>Loads every section's validation status, resiliently: a failure degrades to no icons rather than
    /// failing the page. Pages call it again after saving or clearing, so the icons never show a stale result.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    protected async Task RefreshValidationStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var validationStatus = await profileSectionsApiService.GetProfileVersionValidationStatusAsync(CurrentProfileVersionId, cancellationToken);
            SectionValidationStatus = validationStatus.Sections.ToDictionary(section => section.ProfileSectionId, section => section.IsValid);
            invalidQuestionIds =
            [
                .. validationStatus.Sections
                    .Where(section => section.ProfileSectionId == CurrentSection?.Id)
                    .SelectMany(section => section.InvalidQuestionIds)
            ];
            irrelevantQuestionIds =
            [
                .. validationStatus.Sections
                    .Where(section => section.ProfileSectionId == CurrentSection?.Id)
                    .SelectMany(section => section.IrrelevantQuestionIds)
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileSectionValidationStatusLoadFailed(exception, ProfileId);
            SectionValidationStatus = new Dictionary<Guid, bool>();
            invalidQuestionIds = [];
            irrelevantQuestionIds = [];
        }
    }
}
