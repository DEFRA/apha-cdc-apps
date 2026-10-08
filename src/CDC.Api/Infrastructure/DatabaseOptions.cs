namespace CDC.Api.Infrastructure;

/// <summary>
/// Validated SQL Server connection settings, composed into a connection string by
/// <see cref="SqlConnectionFactory"/>.
/// </summary>
/// <param name="Host">Server host name.</param>
/// <param name="Name">Initial catalog.</param>
/// <param name="User">SQL login. Not required when <paramref name="IntegratedSecurity"/> is <see langword="true"/>.</param>
/// <param name="Password">SQL password, sourced from Secrets Manager. Not required when <paramref name="IntegratedSecurity"/> is <see langword="true"/>.</param>
/// <param name="TrustServerCertificate">Whether to skip certificate validation; local development only.</param>
/// <param name="IntegratedSecurity">Whether to connect with the current Windows identity instead of a SQL
/// login; local LocalDB development only, never set in a deployed environment.</param>
public sealed record DatabaseOptions(
    string Host,
    string Name,
    string? User,
    string? Password,
    bool TrustServerCertificate,
    bool IntegratedSecurity = false);
