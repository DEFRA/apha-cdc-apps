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
/// <param name="TemplateId">The Notify template to use, or <see langword="null"/> for the configured default.</param>
/// <param name="Personalisation">
/// The values for the template's own placeholders. When set, <paramref name="ToName"/>, <paramref name="Subject"/> and
/// <paramref name="Body"/> are not sent; when <see langword="null"/>, they fill the default template's
/// <c>name</c>, <c>subject</c> and <c>body</c> placeholders.
/// </param>
public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string Body,
    string? TemplateId = null,
    IReadOnlyDictionary<string, string>? Personalisation = null)
{
    /// <summary>Creates a message whose text lives in a Notify template with its own placeholders.</summary>
    /// <param name="toAddress">Recipient address.</param>
    /// <param name="templateId">The Notify template to use.</param>
    /// <param name="personalisation">The values for the template's placeholders.</param>
    public static EmailMessage FromTemplate(string toAddress, string templateId, IReadOnlyDictionary<string, string> personalisation) =>
        new(toAddress, string.Empty, string.Empty, string.Empty, templateId, personalisation);
}
