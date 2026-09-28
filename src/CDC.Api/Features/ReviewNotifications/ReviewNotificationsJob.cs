using CDC.Api.Infrastructure;
using Dapper;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>
/// Scaffold entry point for the review-due notification scheduled job (an Amazon EventBridge
/// Scheduler-triggered ECS Scheduled Task reusing this image with a "review-notifications"
/// command override, per the D2R2 LLD Scheduling section - no separate Lambda or image).
/// This only proves the job-mode plumbing (container command, IAM, networking, Parameter
/// Store, database reachability) works end to end; the real review-due query and GOV.UK
/// Notify send logic will replace the body of <see cref="RunAsync"/> once built.
/// </summary>
public sealed class ReviewNotificationsJob(IDbConnectionFactory connectionFactory, ILogger<ReviewNotificationsJob> logger)
{
    /// <summary>Runs the job. Returns true on success, false on failure.</summary>
    /// <param name="cancellationToken">Cancels the database probe.</param>
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        logger.JobStarting();

        try
        {
            using var connection = connectionFactory.CreateConnection();
            var databaseTimeUtc = await connection.QuerySingleAsync<DateTime>(
                new CommandDefinition("SELECT GETUTCDATE()", cancellationToken: cancellationToken));

            logger.DatabaseConnectivityConfirmed(databaseTimeUtc);

            return true;
        }
        catch (Exception ex)
        {
            logger.DatabaseConnectivityFailed(ex);
            return false;
        }
    }
}
