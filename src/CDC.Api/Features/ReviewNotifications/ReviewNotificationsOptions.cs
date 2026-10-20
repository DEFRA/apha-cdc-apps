namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Content settings for the review reminder email, bound from <c>ReviewNotifications</c>.</summary>
public sealed class ReviewNotificationsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ReviewNotifications";

    /// <summary>Internal home page URL linked from the email; the link is omitted when empty.</summary>
    public string InternalHomePageUrl { get; set; } = string.Empty;

    /// <summary>Mailbox users are told to contact for access problems.</summary>
    public string SupportMailbox { get; set; } = "D2R2@defra.gov.uk";
}

/// <summary>Microsoft Graph mail settings, bound from the <c>SmtpSettings</c> section. <see cref="ClientSecret"/> comes from Secrets Manager.</summary>
public sealed class GraphMailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "SmtpSettings";

    /// <summary>Whether sending is switched on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Entra ID tenant.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Application client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Mailbox the email is sent as.</summary>
    public string SenderAddress { get; set; } = string.Empty;
}
