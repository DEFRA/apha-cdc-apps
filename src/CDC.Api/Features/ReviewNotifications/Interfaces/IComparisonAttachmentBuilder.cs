using CDC.Api.Features.ReviewNotifications.Dtos;

namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>Builds the comparison report attached to a user's review reminder.</summary>
public interface IComparisonAttachmentBuilder
{
    /// <summary>
    /// Compares each of the user's draft profile versions with its current published version.
    /// </summary>
    /// <param name="user">The user and their due profile versions.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>One PDF covering every comparison, or <see langword="null"/> when attachments are off or no profile has a published version.</returns>
    Task<EmailAttachment?> BuildAsync(ReviewDueUser user, CancellationToken cancellationToken);
}
