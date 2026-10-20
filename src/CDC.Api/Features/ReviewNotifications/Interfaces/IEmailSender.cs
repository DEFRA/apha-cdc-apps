namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>Sends an HTML email on behalf of the application.</summary>
public interface IEmailSender
{
    /// <summary>Sends one message.</summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>An outgoing HTML email.</summary>
/// <param name="ToAddress">Recipient address.</param>
/// <param name="ToName">Recipient display name.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body.</param>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody);
