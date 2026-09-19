using System.Data;
using Microsoft.Data.SqlClient;

namespace CDC.Api.Infrastructure;

/// <summary>
/// Builds SQL Server connections from the validated <see cref="DatabaseOptions"/> configuration.
/// </summary>
/// <param name="configuration">Application configuration.</param>
public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        var options = StartupChecks.RequireDatabaseOptions(configuration);
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = options.Host,
            InitialCatalog = options.Name,
            UserID = options.User,
            Password = options.Password,
            TrustServerCertificate = options.TrustServerCertificate
        }.ConnectionString;

        return new SqlConnection(connectionString);
    }
}
