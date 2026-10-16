using System.Security.Claims;
using CDC.Common.Contracts;
using CDC.Web.Authorization.Constants;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.UserAdmin;

/// <summary>
/// Maintains global (internal Defra) users and their system roles: a paged list with inline
/// "Add global user" and "Edit global user" panels and a per-row delete confirmation. Replaces the
/// legacy <c>MaintainGlobalUsers.aspx</c>.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanManageGlobalUsers)]
public class GlobalUsersModel : BreadcrumbPageModelBase
{
    /// <summary>Gets the element id of the add/edit panel, so links and posts land on it rather than the top of a long page.</summary>
    public const string PanelId = "global-user-panel";

    private const string PageRoute = "/UserAdmin/GlobalUsers";
    private const int NameMaxLength = 100;
    private const string UserNameField = "UserName";

    private static readonly Action<ILogger, Exception?> LogFailedToLoadUsersMessage =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(LogFailedToLoadUsersMessage)), "Failed to load global users");
    private static readonly Action<ILogger, Exception?> LogFailedToLookUpUserMessage =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(LogFailedToLookUpUserMessage)), "Failed to look up a global user");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToSaveUserMessage =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(3, nameof(LogFailedToSaveUserMessage)), "Failed to save global user '{UserId}'");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToDeleteUserMessage =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(4, nameof(LogFailedToDeleteUserMessage)), "Failed to delete global user '{UserId}'");

    private readonly IGlobalUsersApiService globalUsersApiService;
    private readonly ILogger<GlobalUsersModel> logger;

    public GlobalUsersModel(IGlobalUsersApiService globalUsersApiService, ILogger<GlobalUsersModel> logger)
        : base("Global users")
    {
        this.globalUsersApiService = globalUsersApiService;
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

    /// <summary>Gets or sets the user whose "Edit global user" panel is open, if any.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? EditUserId { get; set; }

    /// <summary>Gets or sets a value indicating whether the "Add global user" panel is open.</summary>
    [BindProperty(SupportsGet = true)]
    public bool AddUser { get; set; }

    /// <summary>Gets or sets the identifier of the user being added, edited or deleted.</summary>
    [BindProperty]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the username: typed in the lookup step, then a locked hidden field.</summary>
    [BindProperty]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Full name field.</summary>
    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Organisation field.</summary>
    [BindProperty]
    public string Organisation { get; set; } = string.Empty;

    /// <summary>Gets or sets the Role field.</summary>
    [BindProperty]
    public GlobalUserRole Role { get; set; }

    /// <summary>Gets or sets the "Subscribe to review emails" checkbox.</summary>
    [BindProperty]
    public bool SubscribedToReviewEmails { get; set; }

    /// <summary>Gets or sets the row version last read for the user. Hidden field.</summary>
    [BindProperty]
    public byte[] LastUpdated { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the form is adding a user rather than editing one. Hidden field.</summary>
    [BindProperty]
    public bool IsNewUser { get; set; }

    /// <summary>Gets or sets the success message shown after a save or delete, carried across the redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets the error message shown after a failed delete, carried across the redirect.</summary>
    [TempData]
    public string? DeleteErrorMessage { get; set; }

    /// <summary>Gets the current page of global users.</summary>
    public IReadOnlyList<GlobalUserDto> Users { get; private set; } = [];

    /// <summary>Gets the total number of result pages.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the users failed to load.</summary>
    public bool HasLoadError { get; private set; }

    /// <summary>Gets a value indicating whether step 1 of the add panel (username lookup) is shown.</summary>
    public bool ShowLookup { get; private set; }

    /// <summary>Gets a value indicating whether the add/edit form (role, names) is shown.</summary>
    public bool ShowForm { get; private set; }

    /// <summary>Gets the non-blocking warning shown when the looked-up username is not yet in the system.</summary>
    public string? LookupWarning { get; private set; }

    /// <summary>Gets the per-field validation errors, keyed by the field's element id.</summary>
    public IReadOnlyDictionary<string, string> FieldErrors { get; private set; } = new Dictionary<string, string>();

    /// <summary>Gets the errors not tied to a single field.</summary>
    public IReadOnlyList<string> GeneralErrors { get; private set; } = [];

    /// <summary>Gets a value indicating whether the page has any validation error to summarise.</summary>
    public bool HasErrors => FieldErrors.Count > 0 || GeneralErrors.Count > 0;

    /// <summary>Gets a value indicating whether the signed-in user is the user in the open panel (their own role is then locked).</summary>
    public bool IsEditingSelf => IsCurrentUser(UserId);

    /// <summary>Gets a value indicating whether <paramref name="id"/> is the signed-in user.</summary>
    /// <param name="id">The user to compare.</param>
    /// <returns><see langword="true"/> when the signed-in user has this identifier.</returns>
    public bool IsCurrentUser(Guid id) =>
        Guid.TryParse(User.FindFirstValue(ExternalUserClaimTypes.InternalUserId), out var currentUserId) && currentUserId == id;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadUsersAsync(cancellationToken);

        if (AddUser)
        {
            ShowLookup = true;
        }
        else if (EditUserId is Guid editId && Users.FirstOrDefault(user => user.Id == editId) is { } user)
        {
            IsNewUser = false;
            UserId = user.Id;
            UserName = user.UserName;
            FullName = user.FullName;
            Organisation = user.Organisation;
            Role = user.Role;
            SubscribedToReviewEmails = user.SubscribedToReviewEmails;
            LastUpdated = user.LastUpdated;
            ShowForm = true;
        }

        return Page();
    }

    /// <summary>Looks the entered username up and, when it can hold a global role, opens the details form. Mirrors <c>btnLookup_Click</c>.</summary>
    public async Task<IActionResult> OnPostLookupUserAsync(CancellationToken cancellationToken)
    {
        AddUser = true;
        ShowLookup = true;
        await LoadUsersAsync(cancellationToken);

        var userName = UserName?.Trim() ?? string.Empty;

        if (userName.Length == 0)
        {
            FieldErrors = new Dictionary<string, string> { [UserNameField] = "Enter a username." };
            return Page();
        }

        GlobalUserLookupResult result;

        try
        {
            result = await globalUsersApiService.LookupAsync(userName, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToLookUpUserMessage(logger, exception);
            GeneralErrors = ["We could not look up this username. Try again later."];
            return Page();
        }

        switch (result.Outcome)
        {
            case GlobalUserLookupOutcome.Blocked:
                FieldErrors = new Dictionary<string, string> { [UserNameField] = "This user cannot be made a global user." };
                break;

            case GlobalUserLookupOutcome.SsoUser:
                FieldErrors = new Dictionary<string, string> { [UserNameField] = "SSO users cannot be given global access rights." };
                break;

            case GlobalUserLookupOutcome.InvalidFormat:
                FieldErrors = new Dictionary<string, string> { [UserNameField] = "Enter a username in the format Domain\\UserId." };
                break;

            default:
                if (result.Outcome == GlobalUserLookupOutcome.NewUser)
                {
                    LookupWarning = "The user is not currently in the profiles system.";
                }

                IsNewUser = true;
                UserId = result.Id;
                UserName = result.UserName;
                FullName = result.FullName;
                Organisation = result.Organisation;
                Role = result.Role;
                SubscribedToReviewEmails = result.SubscribedToReviewEmails;
                LastUpdated = result.LastUpdated;
                ShowLookup = false;
                ShowForm = true;
                break;
        }

        return Page();
    }

    /// <summary>Saves the user being added or edited. Mirrors <c>btnSave_Click</c>.</summary>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        AddUser = IsNewUser;
        EditUserId = IsNewUser ? null : UserId;
        await LoadUsersAsync(cancellationToken);
        ShowForm = true;

        FieldErrors = ValidateForm();

        if (FieldErrors.Count > 0)
        {
            return Page();
        }

        var request = new SaveGlobalUserRequest
        {
            Id = UserId,
            UserName = UserName,
            FullName = FullName.Trim(),
            Organisation = Organisation.Trim(),
            Role = Role,
            SubscribedToReviewEmails = SubscribedToReviewEmails,
            LastUpdated = LastUpdated
        };

        GlobalUserActionResult result;

        try
        {
            result = IsNewUser
                ? await globalUsersApiService.AddAsync(request, cancellationToken)
                : await globalUsersApiService.UpdateAsync(UserId, request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToSaveUserMessage(logger, UserId, exception);
            result = new GlobalUserActionResult
            {
                Outcome = GlobalUserActionOutcome.Error,
                ErrorMessage = "We could not save your changes. Try again later."
            };
        }

        if (result.Outcome == GlobalUserActionOutcome.Success)
        {
            SuccessMessage = "Your changes were successfully saved";

            return RedirectToPage(PageRoute, new { PageNumber, PageSize });
        }

        GeneralErrors = [result.ErrorMessage ?? "We could not save your changes. Try again later."];

        return Page();
    }

    /// <summary>Revokes a user's global role. Mirrors the legacy "DeleteGlobalUser" row command.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (IsCurrentUser(UserId))
        {
            DeleteErrorMessage = "You cannot delete your own global user.";

            return RedirectToPage(PageRoute, new { PageNumber, PageSize });
        }

        try
        {
            var result = await globalUsersApiService.DeleteAsync(UserId, LastUpdated, cancellationToken);

            if (result.Outcome == GlobalUserActionOutcome.Success)
            {
                SuccessMessage = "The user's global role was successfully revoked";
            }
            else
            {
                DeleteErrorMessage = result.ErrorMessage ?? "We could not delete this global user. Try again later.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogFailedToDeleteUserMessage(logger, UserId, exception);
            DeleteErrorMessage = "We could not delete this global user. Try again later.";
        }

        return RedirectToPage(PageRoute, new { PageNumber, PageSize });
    }

    /// <summary>Builds the URL for a results page, preserving <see cref="PageSize"/>.</summary>
    /// <param name="page">The 1-based page.</param>
    /// <returns>The page URL.</returns>
    public string? BuildPageUrl(int page) => Url.Page(PageRoute, new { PageSize, PageNumber = page });

    /// <summary>Builds the URL that opens the "Add global user" panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildAddUrl() => Url.Page(PageRoute, null, new { PageSize, PageNumber, AddUser = true }, null, null, PanelId);

    /// <summary>Builds the URL that opens the "Edit global user" panel for one row.</summary>
    /// <param name="userId">The user to edit.</param>
    /// <returns>The page URL.</returns>
    public string? BuildEditUrl(Guid userId) => Url.Page(PageRoute, null, new { PageSize, PageNumber, EditUserId = userId }, null, null, PanelId);

    /// <summary>Builds the URL that closes any open panel.</summary>
    /// <returns>The page URL.</returns>
    public string? BuildCloseUrl() => Url.Page(PageRoute, new { PageSize, PageNumber });

    private Dictionary<string, string> ValidateForm()
    {
        var errors = new Dictionary<string, string>();

        if (Role is not (GlobalUserRole.ProfileEditor or GlobalUserRole.PolicyProfileUser))
        {
            errors["Role"] = "Select a role.";
        }
        else if (IsEditingSelf && Role != GlobalUserRole.ProfileEditor)
        {
            errors["Role"] = "You cannot remove your own Profile editor rights.";
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            errors["FullName"] = "Enter a full name.";
        }
        else if (FullName.Trim().Length > NameMaxLength)
        {
            errors["FullName"] = $"Full name must be {NameMaxLength} characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(Organisation))
        {
            errors["Organisation"] = "Enter an organisation.";
        }
        else if (Organisation.Trim().Length > NameMaxLength)
        {
            errors["Organisation"] = $"Organisation must be {NameMaxLength} characters or fewer.";
        }

        return errors;
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await globalUsersApiService.GetGlobalUsersAsync(PageNumber, ResolvePageSize(PageSize), cancellationToken);

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
