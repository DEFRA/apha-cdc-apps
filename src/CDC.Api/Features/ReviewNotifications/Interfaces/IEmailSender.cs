namespace CDC.Api.Features.ReviewNotifications.Interfaces;

/// <summary>Sends an email on behalf of the application.</summary>
public interface IEmailSender
{
    /// <summary>Sends one message.</summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>An outgoing email.</summary>
/// <param name="ToAddress">Recipient address.</param>
/// <param name="ToName">Recipient display name.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Body">Body in GOV.UK Notify markdown.</param>
/// <param name="Attachment">Optional file sent as a Notify download link; it needs <c>Notify:AttachmentTemplateId</c>.</param>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string Body, EmailAttachment? Attachment = null);

/// <summary>A file attached to an email.</summary>
/// <param name="FileName">Name shown to the recipient.</param>
/// <param name="Content">File bytes.</param>
public sealed record EmailAttachment(string FileName, byte[] Content);
