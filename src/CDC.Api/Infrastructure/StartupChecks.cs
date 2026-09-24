using Microsoft.Extensions.Configuration;

namespace CDC.Api.Infrastructure;

internal static class StartupChecks
{
    /// <summary>
    /// Fails fast at startup if any DB config value is missing - a clear,
    /// immediate error beats an app that starts and only fails later on
    /// first database use. Host/Name/User/Password come from Parameter
    /// Store as four separate values (no pre-built connection string), so
    /// this validates and packages them for SqlConnectionFactory to
    /// compose. TrustServerCertificate defaults to false (secure by
    /// default, correct for RDS's valid certificate) and should only be
    /// set true locally, for a self-signed dev SQL Server certificate.
    /// </summary>
    public static DatabaseOptions RequireDatabaseOptions(IConfiguration configuration)
    {
        var host = configuration["Database:Host"];
        var name = configuration["Database:Name"];
        var user = configuration["Database:User"];
        var password = configuration["Database:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Database:Host, Database:Name, Database:User and Database:Password must all be configured. " +
                "Locally, set them in appsettings.Development.json; in a deployed environment, check the " +
                "Database__Host / Database__Name / Database__User / Database__Password secret wiring in the " +
                "ECS task definition.");
        }

        var trustServerCertificate = configuration.GetValue("Database:TrustServerCertificate", false);

        return new DatabaseOptions(host, name, user, password, trustServerCertificate);
    }

    /// <summary>
    /// Fails fast at startup if the readiness-check key isn't configured.
    /// ReadinessKeyFilter itself can't tell "not configured" apart from
    /// "wrong key" at request time (both must return an identical 404, so a
    /// scanner can't fingerprint the difference) - without this, a broken
    /// secret wiring would silently make /health/ready return 404 forever
    /// instead of surfacing as an obvious deployment failure.
    /// </summary>
    public static string RequireReadinessKey(IConfiguration configuration)
    {
        var key = configuration["HealthCheck:ReadinessKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "HealthCheck:ReadinessKey is not configured. Locally, set it in appsettings.Development.json; " +
                "in a deployed environment, check the HealthCheck__ReadinessKey secret wiring in the ECS task definition.");
        }

        return key;
    }

    /// <summary>
    /// Fails fast at startup if the placeholder species audit user id isn't configured.
    /// <c>SpeciesTableAuditLog.UserId</c> is a required foreign key to <c>[User].Id</c> - with
    /// no authentication wired up yet, there is no per-request caller to record, so a real
    /// existing user id must be configured here instead of silently failing on first save.
    /// </summary>
    public static Guid RequireSpeciesAuditUserId(IConfiguration configuration)
    {
        var value = configuration["Species:AuditUserId"];

        if (!Guid.TryParse(value, out var auditUserId) || auditUserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Species:AuditUserId must be configured as the id of an existing [User] row. Locally, set it in " +
                "appsettings.Development.json; in a deployed environment, check the Species__AuditUserId wiring " +
                "in the ECS task definition. Replace this with the authenticated caller's id once Entra ID " +
                "authentication is wired up.");
        }

        return auditUserId;
    }
}
