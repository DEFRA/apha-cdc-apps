namespace CDC.Graph.Options;

/// <summary>Configuration bound from the <c>Graph</c> section, controlling Microsoft Graph access using the app's own identity.</summary>
public sealed class GraphOptions
{
    /// <summary>The configuration section name this type binds to ("Graph").</summary>
    public const string SectionName = "Graph";

    /// <summary>Gets or sets a value indicating whether Graph is switched on. When off, nothing is validated and no Graph call is made.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the Entra ID tenant identifier (GUID or verified domain). Required when <see cref="Enabled"/>.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the app registration's application (client) ID. Required when <see cref="Enabled"/>.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the app registration's client secret. Never stored in source control or appsettings.json.</summary>
    public string ClientSecret { get; set; } = string.Empty;
}
