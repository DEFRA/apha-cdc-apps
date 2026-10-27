namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Content settings for the review reminder email, bound from <c>ReviewNotifications</c>.</summary>
public sealed class ReviewNotificationsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ReviewNotifications";

    /// <summary>Internal home page URL linked from the email; the link is omitted when empty.</summary>
    public string InternalHomePageUrl { get; set; } = string.Empty;

    /// <summary>Internet-facing home page URL linked from the email; the link is omitted when empty.</summary>
    public string ExternalHomePageUrl { get; set; } = string.Empty;

    /// <summary>Mailbox users are told to contact for access problems.</summary>
    public string SupportMailbox { get; set; } = "D2R2@defra.gov.uk";

    /// <summary>
    /// Profile role names that make up each reviewer group, keyed by <see cref="ReviewerGroups"/> names
    /// (<c>Policy</c>, <c>AuthorTechnical</c>, <c>VeterinaryAdvisor</c>). A review type with no roles configured is not emailed.
    /// </summary>
    public Dictionary<string, string[]> ReviewerGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>GOV.UK Notify settings, bound from the <c>Notify</c> section. <see cref="ApiKey"/> comes from Secrets Manager.</summary>
public sealed class NotifyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notify";

    /// <summary>Whether sending is switched on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Notify API base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.notifications.service.gov.uk";

    /// <summary>Notify API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Id of the email template that takes <c>name</c>, <c>subject</c> and <c>body</c> personalisation.</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Id of the template that also has a <c>((link_to_file))</c> placeholder, used for emails with an attachment.
    /// Leave empty to send reminders without the comparison report.
    /// </summary>
    public string AttachmentTemplateId { get; set; } = string.Empty;
}
