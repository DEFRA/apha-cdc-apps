using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Pages.UserAdmin;

/// <summary>
/// Lists internal (global) user accounts so an administrator can subscribe them to, or
/// unsubscribe them from, review notification emails. Replaces the review email subscription
/// control on the legacy <c>MaintainGlobalUsers.aspx</c>.
/// </summary>
/// <param name="userAdminApiService">Typed client for the user administration endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class GlobalUsersModel(IUserAdminApiService userAdminApiService, ILogger<GlobalUsersModel> logger)
    : BreadcrumbPageModelBase("Global users")
{
    /// <summary>Gets the global user accounts.</summary>
    public IReadOnlyList<MaintainedUserDto> Users { get; private set; } = [];

    /// <summary>Gets a value indicating whether the user list failed to load.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets a confirmation message shown after a subscription change.</summary>
    public string? SuccessMessage { get; private set; }

    /// <summary>Loads the global user list.</summary>
    /// <param name="updatedUserId">Set by the redirect from the subscription page, to confirm the change.</param>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task OnGetAsync(Guid? updatedUserId, CancellationToken cancellationToken)
    {
        try
        {
            Users = await userAdminApiService.GetGlobalUsersAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.FailedToLoadUsers(exception);
            HasError = true;
            return;
        }

        SuccessMessage = ReviewEmailSubscriptionMessages.ConfirmationFor(Users, updatedUserId);
    }
}
