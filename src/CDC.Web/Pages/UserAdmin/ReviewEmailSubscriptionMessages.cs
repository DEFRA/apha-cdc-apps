using CDC.Web.Models;

namespace CDC.Web.Pages.UserAdmin;

/// <summary>
/// Confirmation wording shared by the global and external user lists after a review email
/// subscription has been changed.
/// </summary>
internal static class ReviewEmailSubscriptionMessages
{
    /// <summary>
    /// Builds the confirmation banner text for a user whose subscription has just changed.
    /// </summary>
    /// <param name="users">The user list just loaded, which carries the stored subscription state.</param>
    /// <param name="updatedUserId">The user whose subscription changed, if any.</param>
    /// <returns>The confirmation text, or <see langword="null"/> when there is nothing to confirm.</returns>
    public static string? ConfirmationFor(IReadOnlyList<MaintainedUserDto> users, Guid? updatedUserId)
    {
        if (updatedUserId is null)
        {
            return null;
        }

        var user = users.FirstOrDefault(candidate => candidate.Id == updatedUserId.Value);

        if (user is null)
        {
            return null;
        }

        return user.SubscribedToReviewEmails
            ? $"{user.FullName} is now subscribed to review emails."
            : $"{user.FullName} is now unsubscribed from review emails.";
    }
}
