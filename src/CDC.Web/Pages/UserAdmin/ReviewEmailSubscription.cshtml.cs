using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.UserAdmin;

/// <summary>
/// Subscribes a user to, or unsubscribes them from, review notification emails. Serves both
/// Maintain global users and Maintain external users, since the capability is identical for
/// each, matching the shared <c>chkSubscribeEmail</c> control on the two legacy screens.
/// </summary>
/// <param name="userAdminApiService">Typed client for the user administration endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class ReviewEmailSubscriptionModel(IUserAdminApiService userAdminApiService, ILogger<ReviewEmailSubscriptionModel> logger)
    : PageModel
{
    private const string GlobalUserType = "global";
    private const string ExternalUserType = "external";

    private bool userLoadFailed;

    /// <summary>Gets or sets the user whose subscription is being changed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets which list the user was selected from: <c>global</c> (the default) or <c>external</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? UserType { get; set; }

    /// <summary>Gets or sets the chosen subscription state. Null until the administrator answers.</summary>
    [BindProperty]
    public bool? SubscribedToReviewEmails { get; set; }

    /// <summary>Gets or sets the base64-encoded row version read with the user, so a concurrent edit is detected.</summary>
    [BindProperty]
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>Gets the user's full name, for the question heading.</summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Gets the user's sign-in name.</summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>Gets the organisation the user belongs to.</summary>
    public string Organisation { get; private set; } = string.Empty;

    /// <summary>Gets the address review notifications would be sent to, when one is recorded.</summary>
    public string? EmailAddress { get; private set; }

    /// <summary>Gets a value indicating whether the user list this page was reached from is the external one.</summary>
    public bool IsExternalUserList => string.Equals(UserType, ExternalUserType, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the Razor Page to return to.</summary>
    public string ListPage => IsExternalUserList ? "/UserAdmin/ExternalUsers" : "/UserAdmin/GlobalUsers";

    /// <summary>Gets the name of the list this page was reached from, for the back link.</summary>
    public string ListPageName => IsExternalUserList ? "External users" : "Global users";

    /// <summary>Loads the user and their current subscription state.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    /// <returns>The page, or a 404 when the user or user type is unknown.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsKnownUserType())
        {
            return NotFound();
        }

        var user = await LoadUserAsync(cancellationToken);

        if (user is null)
        {
            return userLoadFailed ? Page() : NotFound();
        }

        SubscribedToReviewEmails = user.SubscribedToReviewEmails;
        LastUpdated = Convert.ToBase64String(user.LastUpdated);

        return Page();
    }

    /// <summary>Stores the chosen subscription state.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    /// <returns>A redirect back to the user list on success, otherwise the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IsKnownUserType())
        {
            return NotFound();
        }

        if (SubscribedToReviewEmails is null)
        {
            ModelState.AddModelError(
                nameof(SubscribedToReviewEmails),
                "Select whether this user should receive review emails");
        }

        byte[] lastUpdatedBytes = [];

        if (ModelState.IsValid)
        {
            try
            {
                lastUpdatedBytes = Convert.FromBase64String(LastUpdated);
            }
            catch (FormatException)
            {
                ModelState.AddModelError(string.Empty, "The page has expired. Reload the user and try again.");
            }
        }

        if (!ModelState.IsValid)
        {
            await RedisplayAsync(cancellationToken);
            return Page();
        }

        var result = await userAdminApiService.UpdateReviewEmailSubscriptionAsync(
            new UpdateReviewEmailSubscriptionRequest
            {
                UserId = UserId,
                SubscribedToReviewEmails = SubscribedToReviewEmails!.Value,
                LastUpdated = lastUpdatedBytes
            },
            cancellationToken);

        if (result.Outcome != ReviewEmailSubscriptionOutcome.Success)
        {
            logger.FailedToSaveSubscription(UserId, result.Outcome);
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not save this change. Try again later.");
            await RedisplayAsync(cancellationToken);
            return Page();
        }

        logger.SavedSubscription(UserId);

        // Post-redirect-get, so refreshing the confirmation does not resubmit the change.
        return RedirectToPage(ListPage, new { updatedUserId = UserId });
    }

    private bool IsKnownUserType() =>
        string.IsNullOrEmpty(UserType) ||
        string.Equals(UserType, GlobalUserType, StringComparison.OrdinalIgnoreCase) ||
        IsExternalUserList;

    private async Task<MaintainedUserDto?> LoadUserAsync(CancellationToken cancellationToken)
    {
        try
        {
            var user = await userAdminApiService.GetUserAsync(UserId, cancellationToken);

            if (user is not null)
            {
                FullName = user.FullName;
                UserName = user.UserName;
                Organisation = user.Organisation;
                EmailAddress = user.EmailAddress;
            }

            return user;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.FailedToLoadUser(exception, UserId);
            userLoadFailed = true;
            ModelState.AddModelError(string.Empty, "Unable to load this user. Try again later.");

            return null;
        }
    }

    private async Task RedisplayAsync(CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);

        if (user is not null && LastUpdated.Length == 0)
        {
            LastUpdated = Convert.ToBase64String(user.LastUpdated);
        }
    }
}
