using System.Text.Json;
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

    private const string PendingSessionKey = "ExternalUserPendingAccess";

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

    /// <summary>Gets or sets the number of profile rows shown per page in the edit panel, or "All".</summary>
    [BindProperty(SupportsGet = true)]
    public string ProfilePageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets the current 1-based page of the edited user's profiles.</summary>
    [BindProperty(SupportsGet = true)]
    public int ProfilePageNumber { get; set; } = 1;

    /// <summary>Gets or sets the title sort direction of the edited user's profiles: Ascending or Descending.</summary>
    [BindProperty(SupportsGet = true)]
    public string ProfileSortDirection { get; set; } = "Ascending";

    /// <summary>Gets or sets a value indicating whether any staged profile changes are thrown away on load.</summary>
    [BindProperty(SupportsGet = true)]
    public bool DiscardPending { get; set; }

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

    /// <summary>Gets a value indicating whether profiles are sorted by title in descending order.</summary>
    public bool IsProfileSortDescending => string.Equals(ProfileSortDirection, "Descending", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the total number of pages of the edited user's profiles.</summary>
    public int ProfileTotalPages
    {
        get
        {
            var size = ResolvePageSize(ProfilePageSize);
            var count = Access?.Profiles.Count ?? 0;

            return size == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(count / (double)size));
        }
    }

    /// <summary>Gets the current page of the edited user's profiles, sorted by title.</summary>
    public IReadOnlyList<ExternalUserProfileGrantContract> PagedProfiles
    {
        get
        {
            if (Access is null)
            {
                return [];
            }

            var plainTitle = (ExternalUserProfileGrantContract grant) => ProfileTitleHtmlFormatter.ToPlainText(grant.Title);
            var sorted = IsProfileSortDescending
                ? Access.Profiles.OrderByDescending(plainTitle, StringComparer.OrdinalIgnoreCase)
                : Access.Profiles.OrderBy(plainTitle, StringComparer.OrdinalIgnoreCase);
            var size = ResolvePageSize(ProfilePageSize);
            var page = Math.Clamp(ProfilePageNumber, 1, ProfileTotalPages);

            return size == 0 ? [.. sorted] : [.. sorted.Skip((page - 1) * size).Take(size)];
        }
    }

    /// <summary>Gets the <c>aria-sort</c> value for the profile title column.</summary>
    public string ProfileTitleAriaSort => IsProfileSortDescending ? "descending" : "ascending";

    /// <summary>Gets a value indicating whether the users failed to load.</summary>
    public bool HasLoadError { get; private set; }

    /// <summary>Gets a value indicating whether the edited user's profile access failed to load.</summary>
    public bool HasAccessLoadError { get; private set; }

    /// <summary>Gets a value indicating whether added, changed or removed profiles are waiting for OK.</summary>
    public bool HasPendingChanges { get; private set; }

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
        if (DiscardPending)
        {
            HttpContext.Session.Remove(PendingSessionKey);
        }

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

    /// <summary>Saves the review email subscription and every staged profile change.</summary>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (await CommitPendingAsync(cancellationToken))
        {
            SuccessMessage = "Your changes were successfully saved";

            return RedirectToPage(PageRoute, new { PageNumber, PageSize });
        }

        EditUserId = UserId;
        await LoadUsersAsync(cancellationToken);
        EditUser = Users.FirstOrDefault(user => user.Id == UserId);
        SaveError ??= "We could not save your changes. Try again later.";

        if (EditUser is not null)
        {
            await LoadAccessAsync(cancellationToken);
        }

        return Page();
    }

    /// <summary>Stages a role and section permissions grant on a profile; nothing is saved until OK is pressed.</summary>
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

            if (ProfileUserRoles.All(role => role.Id != RoleId))
            {
                AccessError = "Please select a valid role.";
            }
            else if (IsCurrentRoleContributor && sectionIds.Count == 0)
            {
                AccessError = "At least one section must be ticked.";
            }
            else
            {
                var pending = ReadPending();
                pending.Deletes.Remove(ProfileId);
                pending.Adds.RemoveAll(grant => grant.ProfileId == ProfileId);
                pending.Adds.Add(new PendingGrant { ProfileId = ProfileId, RoleId = RoleId, SectionIds = sectionIds });
                WritePending(pending);

                return RedirectToPage(PageRoute, null, new { PageNumber, PageSize, EditUserId = UserId, ProfilePageSize, ProfilePageNumber, ProfileSortDirection }, PanelId);
            }
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

    /// <summary>Stages the removal of the user's access to a profile; nothing is saved until OK is pressed.</summary>
    public async Task<IActionResult> OnPostDeleteAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            var access = await externalUsersApiService.GetProfileAccessAsync(UserId, cancellationToken);
            var pending = ReadPending();

            pending.Adds.RemoveAll(grant => grant.ProfileId == ProfileId);

            if (access?.Profiles.Any(profile => profile.ProfileId == ProfileId) == true && !pending.Deletes.Contains(ProfileId))
            {
                pending.Deletes.Add(ProfileId);
            }

            WritePending(pending);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToDeleteAccessMessage(logger, UserId, exception);
            RemoveErrorMessage = "We could not remove this profile access. Try again later.";
        }

        return RedirectToPage(PageRoute, null, new { PageNumber, PageSize, EditUserId = UserId, ProfilePageSize, ProfilePageNumber, ProfileSortDirection }, PanelId);
    }

    /// <summary>Builds the URL for a results page, preserving <see cref="PageSize"/>.</summary>
    /// <param name="page">The 1-based page.</param>
    /// <returns>The page URL.</returns>
    public string? BuildPageUrl(int page) => Url.Page(PageRoute, new { PageSize, PageNumber = page });

    /// <summary>Builds the URL that opens the edit panel for one row.</summary>
    /// <param name="userId">The user to edit.</param>
    /// <returns>The page URL.</returns>
    public string? BuildEditUrl(Guid userId) => Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = userId, DiscardPending = true }, null, null, PanelId);

    /// <summary>Builds the URL that opens the role and section permissions panel for an existing grant.</summary>
    /// <param name="profileId">The profile whose access is edited.</param>
    /// <returns>The page URL.</returns>
    public string? BuildAccessUrl(Guid profileId) =>
        Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = UserId, ProfilePageSize, ProfilePageNumber, ProfileSortDirection, AccessProfileId = profileId }, null, null, AccessPanelId);

    /// <summary>Builds the URL that closes the role and section permissions panel without saving.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildCloseAccessUrl() =>
        Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = UserId, ProfilePageSize, ProfilePageNumber, ProfileSortDirection }, null, null, PanelId);

    /// <summary>Builds the URL for a page of the edited user's profiles, preserving the page size and sort.</summary>
    /// <param name="page">The 1-based profile page.</param>
    /// <returns>The page URL.</returns>
    public string? BuildProfilePageUrl(int page) =>
        Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = UserId, ProfilePageSize, ProfilePageNumber = page, ProfileSortDirection }, null, null, PanelId);

    /// <summary>Builds the URL that flips the profile title sort direction and returns to the first profile page.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildProfileSortUrl() =>
        Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = UserId, ProfilePageSize, ProfilePageNumber = 1, ProfileSortDirection = IsProfileSortDescending ? "Ascending" : "Descending" }, null, null, PanelId);

    /// <summary>Builds the URL that closes the edit panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildCloseUrl() => Url.Page(PageRoute, new { PageSize, PageNumber, DiscardPending = true });

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

            if (Access is not null)
            {
                await ApplyPendingAsync(cancellationToken);
            }
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

    /// <summary>Overlays the staged adds, changes and removals on the saved profile access so the page shows the pending result.</summary>
    private async Task ApplyPendingAsync(CancellationToken cancellationToken)
    {
        var pending = ReadPending();
        HasPendingChanges = pending.Adds.Count + pending.Deletes.Count > 0;

        if (!HasPendingChanges || Access is null)
        {
            return;
        }

        await LoadRolesAndSectionsAsync(cancellationToken);

        var saved = Access;
        var profiles = saved.Profiles
            .Where(profile => !pending.Deletes.Contains(profile.ProfileId) && pending.Adds.All(grant => grant.ProfileId != profile.ProfileId))
            .ToList();

        foreach (var grant in pending.Adds)
        {
            var role = ProfileUserRoles.FirstOrDefault(candidate => candidate.Id == grant.RoleId);
            var title = saved.Profiles.FirstOrDefault(profile => profile.ProfileId == grant.ProfileId)?.Title
                ?? saved.AvailableProfiles.FirstOrDefault(profile => profile.Id == grant.ProfileId)?.Title
                ?? string.Empty;

            profiles.Add(new ExternalUserProfileGrantContract
            {
                ProfileId = grant.ProfileId,
                Title = title,
                RoleId = grant.RoleId,
                RoleName = role?.Name ?? string.Empty,
                IsContributor = role?.IsContributor ?? false,
                SectionPermissionIds = grant.SectionIds
            });
        }

        var available = saved.AvailableProfiles
            .Where(profile => pending.Adds.All(grant => grant.ProfileId != profile.Id))
            .Concat(saved.Profiles
                .Where(profile => pending.Deletes.Contains(profile.ProfileId))
                .Select(profile => new ProfileOptionContract { Id = profile.ProfileId, Title = profile.Title }))
            .OrderBy(profile => ProfileTitleHtmlFormatter.ToPlainText(profile.Title), StringComparer.OrdinalIgnoreCase);

        Access = new ExternalUserProfileAccessContract { Profiles = profiles, AvailableProfiles = [.. available] };
    }

    /// <summary>Applies the review email subscription and every staged profile change through the API.</summary>
    private async Task<bool> CommitPendingAsync(CancellationToken cancellationToken)
    {
        var pending = ReadPending();

        try
        {
            if (!await externalUsersApiService.SetReviewEmailSubscriptionAsync(UserId, SubscribedToReviewEmails, cancellationToken))
            {
                return false;
            }

            foreach (var profileId in pending.Deletes)
            {
                if (!await externalUsersApiService.DeleteProfileAccessAsync(UserId, profileId, cancellationToken))
                {
                    return false;
                }
            }

            foreach (var grant in pending.Adds)
            {
                var result = await externalUsersApiService.SaveProfileAccessAsync(
                    UserId,
                    grant.ProfileId,
                    new SaveExternalUserProfileAccessContract { RoleId = grant.RoleId, SectionPermissionIds = grant.SectionIds },
                    cancellationToken);

                if (!result.Succeeded)
                {
                    SaveError = result.ErrorMessage;

                    return false;
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToSaveUserMessage(logger, UserId, exception);

            return false;
        }

        HttpContext.Session.Remove(PendingSessionKey);

        return true;
    }

    private PendingAccess ReadPending()
    {
        var json = HttpContext.Session.GetString(PendingSessionKey);
        var pending = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<PendingAccess>(json);

        if (pending is not null && pending.UserId == UserId)
        {
            return pending;
        }

        HttpContext.Session.Remove(PendingSessionKey);

        return new PendingAccess { UserId = UserId };
    }

    private void WritePending(PendingAccess pending)
    {
        if (pending.Adds.Count == 0 && pending.Deletes.Count == 0)
        {
            HttpContext.Session.Remove(PendingSessionKey);

            return;
        }

        HttpContext.Session.SetString(PendingSessionKey, JsonSerializer.Serialize(pending));
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

    private sealed class PendingAccess
    {
        public Guid UserId { get; set; }

        public List<PendingGrant> Adds { get; set; } = [];

        public List<Guid> Deletes { get; set; } = [];
    }

    private sealed class PendingGrant
    {
        public Guid ProfileId { get; set; }

        public Guid RoleId { get; set; }

        public List<Guid> SectionIds { get; set; } = [];
    }
}
