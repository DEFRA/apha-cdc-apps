using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Razor Page listing a profile's contributors, with inline "Add profile contributor" and
/// "Edit profile contributor" panels and a per-row delete confirmation. Replaces the legacy
/// <c>MaintainContributors.aspx</c> grid and <c>pnlProfileContributor</c> panel.
/// </summary>
public class MaintainContributorsModel : PageModel
{
    private const string PageRoute = "/SurveillanceProfiles/MaintainContributors";

    private static readonly Action<ILogger, Guid, Exception?> LogProfileNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogProfileNotFoundMessage)),
            "Profile '{ProfileId}' was not found when loading its contributors");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadProfileTitleMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2, nameof(LogFailedToLoadProfileTitleMessage)),
            "Failed to load the profile title for profile '{ProfileId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadContributorsMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(3, nameof(LogFailedToLoadContributorsMessage)),
            "Failed to load contributors for profile '{ProfileId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadEditPanelMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4, nameof(LogFailedToLoadEditPanelMessage)),
            "Failed to load the edit panel for contributor '{ContributorId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToSaveContributorMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(5, nameof(LogFailedToSaveContributorMessage)),
            "Failed to save contributor '{ContributorId}'");
    private static readonly Action<ILogger, string, Exception?> LogFailedToLookUpUserMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(6, nameof(LogFailedToLookUpUserMessage)),
            "Failed to look up username '{UserName}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToAddContributorMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(7, nameof(LogFailedToAddContributorMessage)),
            "Failed to add contributor '{ContributorId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToDeleteContributorMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(8, nameof(LogFailedToDeleteContributorMessage)),
            "Failed to delete contributor '{ContributorId}'");

    private readonly IApiClient apiClient;
    private readonly IProfileContributorsApiService profileContributorsApiService;
    private readonly IProfileSectionsApiService profileSectionsApiService;
    private readonly ILogger<MaintainContributorsModel> logger;

    public MaintainContributorsModel(
        IApiClient apiClient,
        IProfileContributorsApiService profileContributorsApiService,
        IProfileSectionsApiService profileSectionsApiService,
        ILogger<MaintainContributorsModel> logger)
    {
        this.apiClient = apiClient;
        this.profileContributorsApiService = profileContributorsApiService;
        this.profileSectionsApiService = profileSectionsApiService;
        this.logger = logger;
    }

    /// <summary>Gets or sets the profile whose contributors are being maintained, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the number of rows shown per page, or "All" for no paging.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the contributor whose "Edit profile contributor" panel is open, if any.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? EditContributorId { get; set; }

    /// <summary>Gets or sets the contributor being saved. Hidden field on the edit form.</summary>
    [BindProperty]
    public Guid ContributorId { get; set; }

    /// <summary>Gets or sets the Role field on the edit form.</summary>
    [BindProperty]
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the Full Name field on the edit form. Read-only for an SSO user.</summary>
    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Organisation field on the edit form. Read-only for an SSO user.</summary>
    [BindProperty]
    public string Organisation { get; set; } = string.Empty;

    /// <summary>Gets or sets the checked profile section permission checkboxes on the edit form.</summary>
    [BindProperty]
    public List<Guid> SelectedSectionIds { get; set; } = [];

    /// <summary>Gets or sets the row version last read for the contributor being edited. Hidden field on the edit form.</summary>
    [BindProperty]
    public byte[] LastUpdated { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the "Add profile contributor" panel is open.</summary>
    [BindProperty(SupportsGet = true)]
    public bool AddContributor { get; set; }

    /// <summary>Gets or sets the username entered in the "Add profile contributor" lookup step.
    /// Editable in step 1; a locked, read-only hidden field once the lookup has resolved it.</summary>
    [BindProperty]
    public string LookupUserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the resolved user is an SSO user. Hidden field once resolved.</summary>
    [BindProperty]
    public bool IsSsoUser { get; set; }

    /// <summary>Gets or sets a value indicating whether the resolved user does not yet exist in the
    /// system (the legacy "well-formatted but not currently in the profiles system" fallback).
    /// Hidden field once resolved.</summary>
    [BindProperty]
    public bool IsNewUser { get; set; }

    /// <summary>Gets the static, hardcoded options for the <see cref="PageSize"/> dropdown.</summary>
    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    /// <summary>Gets the profile's title, once loaded.</summary>
    public string? ProfileTitle { get; private set; }

    /// <summary>Gets the current page of contributors.</summary>
    public IReadOnlyList<ContributorDto> Contributors { get; private set; } = [];

    /// <summary>Gets the total number of contributors the profile has, across every page.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>Gets the total number of contributor pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the profile or its contributors failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the contributor shown in the "Edit profile contributor" panel, once loaded.</summary>
    public ContributorEditDto? EditingContributor { get; private set; }

    /// <summary>Gets every role the Role dropdown can offer.</summary>
    public IReadOnlyList<ProfileUserRoleDto> ProfileUserRoles { get; private set; } = [];

    /// <summary>Gets the profile sections shown as permission checkboxes, in display order.</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> PermissionSections { get; private set; } = [];

    /// <summary>Gets the validation errors for the edit panel, if the last save attempt failed.</summary>
    public IReadOnlyList<string> EditValidationErrors { get; private set; } = [];

    /// <summary>Gets the username verification result, once the "Add profile contributor" lookup
    /// step has resolved it (step 2 of the panel).</summary>
    public UserVerificationResultDto? VerifiedUser { get; private set; }

    /// <summary>Gets the blocking error from the username lookup step (an invalid username, or one
    /// that cannot be made a contributor), if any.</summary>
    public string? LookupErrorMessage { get; private set; }

    /// <summary>Gets the non-blocking warning shown when the looked-up username does not exist yet
    /// (the legacy "well-formatted but not currently in the profiles system" case).</summary>
    public string? LookupWarningMessage { get; private set; }

    /// <summary>Gets the validation errors for the add panel, if the last save attempt failed.</summary>
    public IReadOnlyList<string> AddValidationErrors { get; private set; } = [];

    /// <summary>Gets a value indicating whether the currently-selected role requires section
    /// permissions, matching the legacy <c>ddlRole_SelectedIndexChanged</c> show/hide behaviour.</summary>
    public bool IsCurrentRoleContributor => ProfileUserRoles.FirstOrDefault(role => role.Id == RoleId)?.IsContributor ?? false;

    /// <summary>Gets the success message to show after a save, carried across the redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets the error message to show after a failed delete, carried across the redirect.</summary>
    [TempData]
    public string? DeleteErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var profile = await LoadProfileTitleAsync(cancellationToken);

        if (profile is null)
        {
            return HasError ? Page() : NotFound();
        }

        ProfileTitle = profile.ProfileTitle;

        await LoadContributorsAsync(cancellationToken);
        await LoadEditPanelAsync(populateFieldsFromContributor: true, cancellationToken);

        return Page();
    }

    /// <summary>
    /// Saves the contributor being edited: role, (for non-SSO users) full name and organisation,
    /// and the set of profile sections they may edit. Mirrors the legacy <c>btnSave_Click</c>
    /// handler.
    /// </summary>
    public async Task<IActionResult> OnPostSaveContributorAsync(CancellationToken cancellationToken)
    {
        var profile = await LoadProfileTitleAsync(cancellationToken);

        if (profile is null)
        {
            return HasError ? Page() : NotFound();
        }

        ProfileTitle = profile.ProfileTitle;

        var request = new UpdateContributorRequest
        {
            RoleId = RoleId,
            FullName = FullName,
            Organisation = Organisation,
            SectionPermissionIds = SelectedSectionIds,
            LastUpdated = LastUpdated
        };

        UpdateContributorResult result;

        try
        {
            result = await profileContributorsApiService.UpdateContributorAsync(ProfileId, ContributorId, request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToSaveContributorMessage(logger, ContributorId, exception);
            HasError = true;

            return Page();
        }

        if (result.Outcome == ContributorUpdateOutcome.Success)
        {
            SuccessMessage = "Your changes were successfully saved";

            return RedirectToPage(
                PageRoute,
                new { profileId = ProfileId, PageNumber, PageSize });
        }

        EditContributorId = ContributorId;
        EditValidationErrors = [result.ErrorMessage ?? "We could not save this change. Try again later."];

        await LoadContributorsAsync(cancellationToken);
        await LoadEditPanelAsync(populateFieldsFromContributor: false, cancellationToken);

        return Page();
    }

    /// <summary>
    /// Verifies the entered username and, if valid, loads the Role/Full Name/Organisation/
    /// permissions fields for step 2 of the "Add profile contributor" panel. Mirrors the legacy
    /// <c>btnLookup_Click</c> handler.
    /// </summary>
    public async Task<IActionResult> OnPostLookupUserAsync(CancellationToken cancellationToken)
    {
        var profile = await LoadProfileTitleAsync(cancellationToken);

        if (profile is null)
        {
            return HasError ? Page() : NotFound();
        }

        ProfileTitle = profile.ProfileTitle;
        AddContributor = true;

        await LoadContributorsAsync(cancellationToken);

        try
        {
            var verification = await profileContributorsApiService.VerifyContributorUsernameAsync(LookupUserName, cancellationToken);

            switch (verification.Outcome)
            {
                case UserVerificationOutcome.Blocked:
                    LookupErrorMessage = "This user cannot be made a contributor.";
                    break;

                case UserVerificationOutcome.InvalidFormat:
                    LookupErrorMessage = "This user is not currently in the profiles system.";
                    break;

                case UserVerificationOutcome.NewUser:
                    LookupWarningMessage = "The user is not currently in the profiles system.";
                    VerifiedUser = verification;
                    IsNewUser = true;
                    IsSsoUser = false;
                    ContributorId = Guid.NewGuid();
                    RoleId = Guid.Empty;
                    FullName = string.Empty;
                    Organisation = string.Empty;
                    SelectedSectionIds = [];
                    await LoadRolesAndPermissionSectionsAsync(cancellationToken);
                    break;

                default:
                    await PopulateExistingUserAddFieldsAsync(verification, cancellationToken);
                    break;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLookUpUserMessage(logger, LookupUserName, exception);
            HasError = true;
        }

        return Page();
    }

    /// <summary>
    /// Saves the contributor being added: an existing global user not yet on this profile, or a
    /// brand-new username not yet known to the system. Mirrors the legacy <c>btnSave_Click</c>
    /// handler's "Add" path.
    /// </summary>
    public async Task<IActionResult> OnPostAddContributorAsync(CancellationToken cancellationToken)
    {
        var profile = await LoadProfileTitleAsync(cancellationToken);

        if (profile is null)
        {
            return HasError ? Page() : NotFound();
        }

        ProfileTitle = profile.ProfileTitle;

        var request = new AddContributorRequest
        {
            ContributorId = ContributorId,
            UserName = LookupUserName,
            IsSsoUser = IsSsoUser,
            RoleId = RoleId,
            FullName = FullName,
            Organisation = Organisation,
            SectionPermissionIds = SelectedSectionIds
        };

        AddContributorResult result;

        try
        {
            result = await profileContributorsApiService.AddContributorAsync(ProfileId, request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToAddContributorMessage(logger, ContributorId, exception);
            HasError = true;

            return Page();
        }

        if (result.Outcome == ContributorAddOutcome.Success)
        {
            SuccessMessage = "The contributor was successfully added";

            return RedirectToPage(
                PageRoute,
                new { profileId = ProfileId, PageNumber, PageSize });
        }

        AddContributor = true;
        VerifiedUser = new UserVerificationResultDto
        {
            Outcome = IsNewUser ? UserVerificationOutcome.NewUser : UserVerificationOutcome.ExistingUser,
            UserId = ContributorId
        };
        AddValidationErrors = [result.ErrorMessage ?? "We could not add this contributor. Try again later."];

        await LoadContributorsAsync(cancellationToken);

        try
        {
            await LoadRolesAndPermissionSectionsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToAddContributorMessage(logger, ContributorId, exception);
            HasError = true;
        }

        return Page();
    }

    /// <summary>
    /// Removes a contributor from a profile. Mirrors the legacy
    /// <c>grdProfileContributors_RowCommand</c> "DeleteContributor" handler.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteContributorAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await profileContributorsApiService.DeleteContributorAsync(ProfileId, ContributorId, LastUpdated, cancellationToken);

            if (result.Outcome == ContributorDeleteOutcome.Success)
            {
                SuccessMessage = "The user was successfully removed from the list of contributors";
            }
            else
            {
                DeleteErrorMessage = result.ErrorMessage ?? "We could not remove this contributor. Try again later.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToDeleteContributorMessage(logger, ContributorId, exception);
            DeleteErrorMessage = "We could not remove this contributor. Try again later.";
        }

        return RedirectToPage(PageRoute, new { profileId = ProfileId, PageNumber, PageSize });
    }

    /// <summary>Loads an existing global user's current detail on this profile (if any) for step 2
    /// of the "Add profile contributor" panel, reusing the same read as the edit panel.</summary>
    private async Task PopulateExistingUserAddFieldsAsync(UserVerificationResultDto verification, CancellationToken cancellationToken)
    {
        var userId = verification.UserId!.Value;
        var contributor = await profileContributorsApiService.GetContributorForEditAsync(ProfileId, userId, cancellationToken);

        if (contributor is null)
        {
            // Extremely unlikely race: the global user was removed between the lookup and this read.
            LookupErrorMessage = "This user is not currently in the profiles system.";
            return;
        }

        VerifiedUser = verification;
        IsNewUser = false;
        ContributorId = contributor.Id;
        LookupUserName = contributor.UserName;
        IsSsoUser = contributor.IsSsoUser;
        RoleId = contributor.RoleId;
        FullName = contributor.FullName;
        Organisation = contributor.Organisation;
        SelectedSectionIds = [.. contributor.SectionPermissionIds];

        await LoadRolesAndPermissionSectionsAsync(cancellationToken);
    }

    /// <summary>Builds the querystring URL for a results page link, preserving <see cref="PageSize"/>.</summary>
    public string? BuildPageUrl(int page) =>
        Url.Page(PageRoute, new { ProfileId, PageSize, PageNumber = page });

    /// <summary>Builds the querystring URL that opens the "Add profile contributor" panel.</summary>
    public string? BuildAddUrl() =>
        Url.Page(PageRoute, new { ProfileId, PageSize, PageNumber, AddContributor = true });

    /// <summary>Builds the querystring URL that opens the "Edit profile contributor" panel for one row.</summary>
    public string? BuildEditUrl(Guid contributorId) =>
        Url.Page(PageRoute, new { ProfileId, PageSize, PageNumber, EditContributorId = contributorId });

    /// <summary>Builds the querystring URL that closes the "Edit profile contributor" panel.</summary>
    public string? BuildCloseEditUrl() =>
        Url.Page(PageRoute, new { ProfileId, PageSize, PageNumber });

    /// <summary>Uses the same profile retrieval as Manage profile (<c>GET /api/profiles/{profileId}/manage</c>),
    /// so the page title always matches what Manage profile shows for this profile.</summary>
    private async Task<ManageProfileViewModel?> LoadProfileTitleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);

            if (profile is null)
            {
                LogProfileNotFoundMessage(logger, ProfileId, null);
            }

            return profile;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadProfileTitleMessage(logger, ProfileId, exception);
            HasError = true;

            return null;
        }
    }

    private async Task LoadContributorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pageSize = ResolvePageSize(PageSize);
            var result = await profileContributorsApiService.GetProfileContributorsAsync(
                ProfileId, PageNumber, pageSize, cancellationToken);

            TotalRecords = result.TotalRecords;
            TotalPages = result.TotalPages;
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
            Contributors = result.Items;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadContributorsMessage(logger, ProfileId, exception);
            HasError = true;
            Contributors = [];
        }
    }

    /// <summary>
    /// Loads the data the "Edit profile contributor" panel needs, when <see cref="EditContributorId"/>
    /// is set. On a fresh GET the edit fields are populated from the loaded contributor; when
    /// redisplaying a failed save, the user's submitted values are kept instead.
    /// </summary>
    private async Task LoadEditPanelAsync(bool populateFieldsFromContributor, CancellationToken cancellationToken)
    {
        if (EditContributorId is not Guid contributorId)
        {
            return;
        }

        try
        {
            var contributor = await profileContributorsApiService.GetContributorForEditAsync(ProfileId, contributorId, cancellationToken);

            if (contributor is null)
            {
                EditContributorId = null;
                return;
            }

            EditingContributor = contributor;
            await LoadRolesAndPermissionSectionsAsync(cancellationToken);

            if (populateFieldsFromContributor)
            {
                ContributorId = contributor.Id;
                RoleId = contributor.RoleId;
                FullName = contributor.FullName;
                Organisation = contributor.Organisation;
                SelectedSectionIds = [.. contributor.SectionPermissionIds];
                LastUpdated = contributor.LastUpdated;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadEditPanelMessage(logger, contributorId, exception);
            HasError = true;
        }
    }

    /// <summary>Loads the Role dropdown options and the permission checkbox sections; shared by the
    /// "Edit profile contributor" and "Add profile contributor" panels.</summary>
    private async Task LoadRolesAndPermissionSectionsAsync(CancellationToken cancellationToken)
    {
        ProfileUserRoles = await profileContributorsApiService.GetProfileUserRolesAsync(cancellationToken);

        var metadata = await profileSectionsApiService.GetProfileQuestionnaireMetadataAsync(cancellationToken);
        PermissionSections = [.. metadata.Sections.OrderBy(section => section.SectionNumber)];
    }

    /// <summary>Resolves the "Items per page" selection to a page size, where 0 means "All" (no paging).</summary>
    private static int ResolvePageSize(string pageSize)
    {
        if (string.Equals(pageSize, "All", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(pageSize, out var parsedPageSize) ? parsedPageSize : 10;
    }
}

