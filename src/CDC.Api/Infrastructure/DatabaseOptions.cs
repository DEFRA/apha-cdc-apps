namespace CDC.Api.Infrastructure;

/// <summary>
/// Validated SQL Server connection settings, composed into a connection string by
/// <see cref="SqlConnectionFactory"/>.
/// </summary>
/// <param name="Host">Server host name.</param>
/// <param name="Name">Initial catalog.</param>
/// <param name="User">SQL login.</param>
/// <param name="Password">SQL password, sourced from Secrets Manager.</param>
/// <param name="TrustServerCertificate">Whether to skip certificate validation; local development only.</param>
public sealed record DatabaseOptions(string Host, string Name, string User, string Password, bool TrustServerCertificate);
