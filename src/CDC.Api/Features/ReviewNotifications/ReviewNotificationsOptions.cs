namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Content settings for review reminder emails, bound from the <c>ReviewNotifications</c> section.</summary>
public sealed class ReviewNotificationsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ReviewNotifications";

    /// <summary>Link to the internal D2R2 home page; the link is omitted from the email when blank or not an absolute http(s) URL.</summary>
    public string? InternalHomePageUrl { get; set; }

    /// <summary>The mailbox users are told to contact about access problems.</summary>
    public string? SupportMailbox { get; set; }
}
