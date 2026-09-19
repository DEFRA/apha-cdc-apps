using System.Data;

namespace CDC.Api.Infrastructure;

/// <summary>
/// Single place that knows how to open a SQL Server connection. Dapper works
/// against IDbConnection and expects the caller to own its lifetime, so this
/// creates one per unit of work rather than sharing a long-lived connection.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates a new, unopened connection. The caller owns its lifetime.</summary>
    /// <returns>A connection to the Surveillance Profiles database.</returns>
    IDbConnection CreateConnection();
}
