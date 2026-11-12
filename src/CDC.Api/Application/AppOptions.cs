namespace CDC.Api.Application;

/// <summary>Settings that describe the application itself, bound from the <c>App</c> section.</summary>
public sealed class AppOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "App";

    /// <summary>The address users open D2R2 at. It is linked from the emails; reminders omit the link when it is empty and login instructions are not sent without it.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
