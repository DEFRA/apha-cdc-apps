using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IReviewNotificationRepository"/>.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ReviewNotificationRepository(IDbConnectionFactory connectionFactory, ILogger<ReviewNotificationRepository> logger)
    : IReviewNotificationRepository
{
    /// <summary>
    /// The legacy view already restricts the result to users whose
    /// <c>SubscribedToReviewEmails</c> flag is set and who have an email address recorded, so
    /// unsubscribing a user removes them from the recipient list.
    /// </summary>
    private const string SelectRecipientsDueReviewEmail =
        "SELECT [Id], [UserName], [EmailAddress], [FullName] FROM [dbo].[vwReviewEmailUsers]";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReviewEmailRecipient>> GetRecipientsDueReviewEmailAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<RecipientRow>(new CommandDefinition(
            SelectRecipientsDueReviewEmail,
            cancellationToken: cancellationToken));

        return
        [
            .. rows.Select(row => new ReviewEmailRecipient
            {
                Id = row.Id,
                UserName = row.UserName ?? string.Empty,
                FullName = row.FullName ?? string.Empty,
                EmailAddress = row.EmailAddress ?? string.Empty
            })
        ];
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ReviewNotificationRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.DatabaseConnectivityFailed(exception);
            await dbConnection.DisposeAsync();
            throw;
        }
    }

    private sealed class RecipientRow
    {
        public Guid Id { get; init; }

        public string? UserName { get; init; }

        public string? FullName { get; init; }

        public string? EmailAddress { get; init; }
    }
}
