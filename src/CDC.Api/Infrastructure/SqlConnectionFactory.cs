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
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Host,
            InitialCatalog = options.Name,
            TrustServerCertificate = options.TrustServerCertificate
        };

        if (options.IntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = options.User;
            builder.Password = options.Password;
        }

        return new SqlConnection(builder.ConnectionString);
    }
}
