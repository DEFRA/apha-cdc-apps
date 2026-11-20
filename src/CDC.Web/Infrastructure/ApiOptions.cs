namespace CDC.Web.Infrastructure;

/// <summary>Strongly typed configuration for calling CDC.Api, bound from the <c>Api</c> section.</summary>
public sealed class ApiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Api";

    /// <summary>Base URL of CDC.Api. Required and validated as an absolute http(s) URL at startup in Program.cs.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shared key CDC.Web authenticates to CDC.Api with (environment variable <c>Api__ServiceKey</c>, from Secrets Manager).
    /// Must equal CDC.Api's <c>ServiceAuth:ApiKey</c>. Required; checked at startup in Program.cs.
    /// </summary>
    public string ServiceKey { get; set; } = string.Empty;
}
