namespace CDC.Web.Infrastructure;

/// <summary>Strongly typed configuration for authenticating to CDC.Api, bound from the <c>ServiceAuth</c> section.</summary>
public sealed class ServiceAuthOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ServiceAuth";

    /// <summary>
    /// Shared key CDC.Web authenticates to CDC.Api with (environment variable <c>ServiceAuth__ApiKey</c>, from AWS Parameter Store).
    /// Must equal CDC.Api's <c>Api:ServiceKey</c>. Required; checked at startup in Program.cs.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
