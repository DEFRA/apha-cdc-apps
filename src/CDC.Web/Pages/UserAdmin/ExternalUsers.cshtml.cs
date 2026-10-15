using CDC.Common.Contracts;
using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.UserAdmin;

/// <summary>
/// Maintains external users: a paged list with an inline panel to grant profile access (role and section
/// permissions) and to set the review email subscription. Replaces the legacy <c>MaintainExternalUsers.aspx</c>.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanManageExternalUsers)]
public class ExternalUsersModel : BreadcrumbPageModelBase
{
    /// <summary>Gets the element id of the edit panel, so links and posts land on it rather than the top of a long page.</summary>
    public const string PanelId = "external-user-panel";

    /// <summary>Gets the element id of the role and section permissions panel.</summary>
    public const string AccessPanelId = "external-user-access-panel";

    private const string PageRoute = "/UserAdmin/ExternalUsers";

    private static readonly Action<ILogger, Exception?> LogFailedToLoadUsersMessage =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(LogFailedToLoadUsersMessage)), "Failed to load external users");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToSaveUserMessage =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(2, nameof(LogFailedToSaveUserMessage)), "Failed to save external user '{UserId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadAccessMessage =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(3, nameof(LogFailedToLoadAccessMessage)), "Failed to load profile access for external user '{UserId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToSaveAccessMessage =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(4, nameof(LogFailedToSaveAccessMessage)), "Failed to save profile access for external user '{UserId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToDeleteAccessMessage =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(5, nameof(LogFailedToDeleteAccessMessage)), "Failed to remove profile access for external user '{UserId}'");

    private readonly IExternalUsersApiService externalUsersApiService;
    private readonly IProfileContributorsApiService profileContributorsApiService;
    private readonly IProfileSectionsApiService profileSectionsApiService;
    private readonly ILogger<ExternalUsersModel> logger;

    public ExternalUsersModel(
        IExternalUsersApiService externalUsersApiService,
        IProfileContributorsApiService profileContributorsApiService,
        IProfileSectionsApiService profileSectionsApiService,
        ILogger<ExternalUsersModel> logger)
        : base("External users")
    {
        this.externalUsersApiService = externalUsersApiService;
        this.profileContributorsApiService = profileContributorsApiService;
        this.profileSectionsApiService = profileSectionsApiService;
        this.logger = logger;
    }

    /// <summary>Gets the static options for the "Items per page" dropdown.</summary>
    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    /// <summary>Gets or sets the number of rows shown per page, or "All" for no paging.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets the user whose edit panel is open, if any.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? EditUserId { get; set; }

    /// <summary>Gets or sets the profile whose role and section permissions are being edited, if any.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? AccessProfileId { get; set; }

    /// <summary>Gets or sets the profile being added for the user, if any.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? AddProfileId { get; set; }

    /// <summary>Gets or sets the identifier of the user being saved.</summary>
    [BindProperty]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the profile whose access is being saved or removed.</summary>
    [BindProperty]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the role selected for the profile.</summary>
    [BindProperty]
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the ticked profile section permissions.</summary>
    [BindProperty]
    public List<Guid> SelectedSectionIds { get; set; } = [];

    /// <summary>Gets or sets the "Subscribe to review emails" checkbox.</summary>
    [BindProperty]
    public bool SubscribedToReviewEmails { get; set; }

    /// <summary>Gets or sets the success message shown after a save, carried across the redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets the error shown after a failed removal, carried across the redirect.</summary>
    [TempData]
    public string? RemoveErrorMessage { get; set; }

    /// <summary>Gets the current page of external users.</summary>
    public IReadOnlyList<MaintainedExternalUserDto> Users { get; private set; } = [];

    /// <summary>Gets the user in the open edit panel, if any.</summary>
    public MaintainedExternalUserDto? EditUser { get; private set; }

    /// <summary>Gets the profiles the edited user can access and those available to add, once loaded.</summary>
    public ExternalUserProfileAccessContract? Access { get; private set; }

    /// <summary>Gets the profile access being edited, when changing an existing grant.</summary>
    public ExternalUserProfileGrantContract? EditingGrant { get; private set; }

    /// <summary>Gets the profile being added, when granting new access.</summary>
    public ProfileOptionContract? AddingProfile { get; private set; }

    /// <summary>Gets every role the role dropdown offers.</summary>
    public IReadOnlyList<ProfileUserRoleDto> ProfileUserRoles { get; private set; } = [];

    /// <summary>Gets the profile sections shown as permission checkboxes, in display order.</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> PermissionSections { get; private set; } = [];

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the users failed to load.</summary>
    public bool HasLoadError { get; private set; }

    /// <summary>Gets a value indicating whether the edited user's profile access failed to load.</summary>
    public bool HasAccessLoadError { get; private set; }

    /// <summary>Gets the error shown when saving the subscription failed.</summary>
    public string? SaveError { get; private set; }

    /// <summary>Gets the error shown when saving role and section permissions failed.</summary>
    public string? AccessError { get; private set; }

    /// <summary>Gets a value indicating whether the role and section permissions panel is open.</summary>
    public bool IsAccessPanelOpen => EditingGrant is not null || AddingProfile is not null;

    /// <summary>Gets a value indicating whether the selected role carries section permissions.</summary>
    public bool IsCurrentRoleContributor => ProfileUserRoles.FirstOrDefault(role => role.Id == RoleId)?.IsContributor ?? false;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadUsersAsync(cancellationToken);

        if (EditUserId is Guid editId && Users.FirstOrDefault(user => user.Id == editId) is { } user)
        {
            EditUser = user;
            UserId = user.Id;
            SubscribedToReviewEmails = user.SubscribedToReviewEmails;

            await LoadAccessAsync(cancellationToken);
            await OpenAccessPanelAsync(populateFromGrant: true, cancellationToken);
        }

        return Page();
    }

    /// <summary>Subscribes or unsubscribes the user in the edit panel from review notification emails.</summary>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var saved = false;

        try
        {
            saved = await externalUsersApiService.SetReviewEmailSubscriptionAsync(UserId, SubscribedToReviewEmails, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToSaveUserMessage(logger, UserId, exception);
        }

        if (saved)
        {
            SuccessMessage = "Your changes were successfully saved";

            return RedirectToPage(PageRoute, new { PageNumber, PageSize });
        }

        EditUserId = UserId;
        await LoadUsersAsync(cancellationToken);
        EditUser = Users.FirstOrDefault(user => user.Id == UserId);
        SaveError = "We could not save your changes. Try again later.";

        if (EditUser is not null)
        {
            await LoadAccessAsync(cancellationToken);
        }

        return Page();
    }

    /// <summary>Grants the user a role and section permissions on a profile, or changes an existing grant.</summary>
    public async Task<IActionResult> OnPostSaveAccessAsync(CancellationToken cancellationToken)
    {
        EditUserId = UserId;
        await LoadUsersAsync(cancellationToken);
        EditUser = Users.FirstOrDefault(user => user.Id == UserId);

        if (EditUser is null)
        {
            return RedirectToPage(PageRoute, new { PageNumber, PageSize });
        }

        SubscribedToReviewEmails = EditUser.SubscribedToReviewEmails;

        try
        {
            await LoadRolesAndSectionsAsync(cancellationToken);

            var knownSectionIds = PermissionSections.Select(section => section.Id).ToHashSet();
            var sectionIds = IsCurrentRoleContributor
                ? SelectedSectionIds.Where(knownSectionIds.Contains).Distinct().ToList()
                : [];

            var result = await externalUsersApiService.SaveProfileAccessAsync(
                UserId,
                ProfileId,
                new SaveExternalUserProfileAccessContract { RoleId = RoleId, SectionPermissionIds = sectionIds },
                cancellationToken);

            if (result.Succeeded)
            {
                SuccessMessage = "Your changes were successfully saved";

                return RedirectToPage(PageRoute, null, new { PageNumber, PageSize, EditUserId = UserId }, PanelId);
            }

            AccessError = result.ErrorMessage;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToSaveAccessMessage(logger, UserId, exception);
            AccessError = "We could not save your changes. Try again later.";
        }

        await LoadAccessAsync(cancellationToken);

        // Re-open the panel for the same profile, keeping what the user entered.
        AccessProfileId = Access?.Profiles.Any(profile => profile.ProfileId == ProfileId) == true ? ProfileId : null;
        AddProfileId = AccessProfileId is null ? ProfileId : null;
        await OpenAccessPanelAsync(populateFromGrant: false, cancellationToken);

        return Page();
    }

    /// <summary>Removes the user's access to a profile.</summary>
    public async Task<IActionResult> OnPostDeleteAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await externalUsersApiService.DeleteProfileAccessAsync(UserId, ProfileId, cancellationToken))
            {
                SuccessMessage = "Profile access was successfully removed";
            }
            else
            {
                RemoveErrorMessage = "We could not remove this profile access. Try again later.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToDeleteAccessMessage(logger, UserId, exception);
            RemoveErrorMessage = "We could not remove this profile access. Try again later.";
        }

        return RedirectToPage(PageRoute, null, new { PageNumber, PageSize, EditUserId = UserId }, PanelId);
    }

    /// <summary>Builds the URL for a results page, preserving <see cref="PageSize"/>.</summary>
    /// <param name="page">The 1-based page.</param>
    /// <returns>The page URL.</returns>
    public string? BuildPageUrl(int page) => Url.Page(PageRoute, new { PageSize, PageNumber = page });

    /// <summary>Builds the URL that opens the edit panel for one row.</summary>
    /// <param name="userId">The user to edit.</param>
    /// <returns>The page URL.</returns>
    public string? BuildEditUrl(Guid userId) => Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = userId }, null, null, PanelId);

    /// <summary>Builds the URL that opens the role and section permissions panel for an existing grant.</summary>
    /// <param name="profileId">The profile whose access is edited.</param>
    /// <returns>The page URL.</returns>
    public string? BuildAccessUrl(Guid profileId) =>
        Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = UserId, AccessProfileId = profileId }, null, null, AccessPanelId);

    /// <summary>Builds the URL that closes the role and section permissions panel without saving.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildCloseAccessUrl() => Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = UserId }, null, null, PanelId);

    /// <summary>Builds the URL that closes the edit panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildCloseUrl() => Url.Page(PageRoute, new { PageSize, PageNumber });

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await externalUsersApiService.GetExternalUsersAsync(PageNumber, ResolvePageSize(PageSize), cancellationToken);

            TotalPages = result.TotalPages;
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
            Users = result.Items;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadUsersMessage(logger, exception);
            HasLoadError = true;
            Users = [];
        }
    }

    private async Task LoadAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            Access = await externalUsersApiService.GetProfileAccessAsync(UserId, cancellationToken);
            HasAccessLoadError = Access is null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadAccessMessage(logger, UserId, exception);
            HasAccessLoadError = true;
        }
    }

    /// <summary>
    /// Opens the role and section permissions panel for <see cref="AccessProfileId"/> or <see cref="AddProfileId"/>.
    /// When redisplaying a failed save the user's submitted role and sections are kept instead of the saved ones.
    /// </summary>
    private async Task OpenAccessPanelAsync(bool populateFromGrant, CancellationToken cancellationToken)
    {
        if (Access is null)
        {
            return;
        }

        if (AccessProfileId is Guid grantedId && Access.Profiles.FirstOrDefault(profile => profile.ProfileId == grantedId) is { } grant)
        {
            EditingGrant = grant;

            if (populateFromGrant)
            {
                RoleId = grant.RoleId;
                SelectedSectionIds = [.. grant.SectionPermissionIds];
            }
        }
        else if (AddProfileId is Guid addId && Access.AvailableProfiles.FirstOrDefault(profile => profile.Id == addId) is { } option)
        {
            AddingProfile = option;

            if (populateFromGrant)
            {
                RoleId = Guid.Empty;
                SelectedSectionIds = [];
            }
        }
        else
        {
            return;
        }

        ProfileId = EditingGrant?.ProfileId ?? AddingProfile!.Id;

        try
        {
            await LoadRolesAndSectionsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLoadAccessMessage(logger, UserId, exception);
            HasAccessLoadError = true;
            EditingGrant = null;
            AddingProfile = null;
        }
    }

    private async Task LoadRolesAndSectionsAsync(CancellationToken cancellationToken)
    {
        if (ProfileUserRoles.Count > 0)
        {
            return;
        }

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

        return int.TryParse(pageSize, out var parsed) && parsed > 0 ? parsed : 10;
    }
}
