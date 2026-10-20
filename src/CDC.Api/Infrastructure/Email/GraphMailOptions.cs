namespace CDC.Api.Infrastructure.Email;

/// <summary>
/// Microsoft Graph mail settings, bound from the <c>SmtpSettings</c> section. <see cref="ClientSecret"/>
/// arrives as <c>SmtpSettings__ClientSecret</c> from Secrets Manager and must never be stored in a file.
/// </summary>
public sealed class GraphMailOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "SmtpSettings";

    /// <summary>The Entra ID tenant.</summary>
    public string? TenantId { get; set; }

    /// <summary>The application (client) id of the app registration holding the Mail.Send permission.</summary>
    public string? ClientId { get; set; }

    /// <summary>The app registration's client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>The mailbox emails are sent as.</summary>
    public string? SenderAddress { get; set; }

    /// <summary>When false the scheduled job logs a warning and sends nothing.</summary>
    public bool Enabled { get; set; }

    /// <summary>Throws <see cref="InvalidOperationException"/> naming each missing setting.</summary>
    public void Validate()
    {
        var missing = new List<string>();
        AddIfBlank(missing, TenantId, nameof(TenantId));
        AddIfBlank(missing, ClientId, nameof(ClientId));
        AddIfBlank(missing, ClientSecret, nameof(ClientSecret));
        AddIfBlank(missing, SenderAddress, nameof(SenderAddress));

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing mail settings: {string.Join(", ", missing.Select(name => $"{SectionName}:{name}"))}. " +
                "Check the SmtpSettings__* wiring in the ECS task definition.");
        }
    }

    private static void AddIfBlank(List<string> missing, string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }
}
