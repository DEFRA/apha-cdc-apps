using CDC.Api.Features.ReviewNotifications.Dtos;

namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>Delivers review reminder emails.</summary>
public interface IReviewEmailSender
{
    /// <summary>Throws <see cref="InvalidOperationException"/> when the mail settings are incomplete.</summary>
    void ValidateConfiguration();

    /// <summary>Sends one email.</summary>
    /// <param name="message">The email to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task SendAsync(ReviewEmailMessage message, CancellationToken cancellationToken);
}
