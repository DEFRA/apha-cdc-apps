using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.BespokeReportTemplates;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IBespokeReportTemplateRepository"/>. The legacy NPoco data
/// service used the two template tables directly and there are no stored procedures for them, so
/// every command is a parameterised text command.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class BespokeReportTemplateRepository(IDbConnectionFactory connectionFactory, ILogger<BespokeReportTemplateRepository> logger)
    : IBespokeReportTemplateRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<BespokeReportTemplate>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<TemplateRow>(Text(BespokeReportTemplateSql.GetAll, null, null, cancellationToken));

            return [.. rows.Select(row => new BespokeReportTemplate { Id = row.Id, Title = row.Title ?? string.Empty })];
        }
        catch (DbException exception)
        {
            logger.CommandFailed(exception, nameof(GetAllAsync));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<BespokeReportTemplate?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<TemplateRow>(Text(BespokeReportTemplateSql.GetById, new { Id = id }, null, cancellationToken));

            if (row is null)
            {
                return null;
            }

            var checkedItems = await connection.QueryAsync<string>(Text(BespokeReportTemplateSql.GetCheckedItems, new { Id = id }, null, cancellationToken));

            return new BespokeReportTemplate { Id = row.Id, Title = row.Title ?? string.Empty, CheckedItems = [.. checkedItems] };
        }
        catch (DbException exception)
        {
            logger.CommandFailed(exception, nameof(GetAsync));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<BespokeReportTemplate?> GetByTitleAsync(string title, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QueryFirstOrDefaultAsync<TemplateRow>(Text(BespokeReportTemplateSql.GetByTitle, new { Title = title }, null, cancellationToken));

            return row is null ? null : new BespokeReportTemplate { Id = row.Id, Title = row.Title ?? string.Empty };
        }
        catch (DbException exception)
        {
            logger.CommandFailed(exception, nameof(GetByTitleAsync));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Guid> InsertAsync(string title, IReadOnlyList<string> checkedItems, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var id = Guid.NewGuid();

            await connection.ExecuteAsync(Text(BespokeReportTemplateSql.InsertTemplate, new { Id = id, Title = title }, transaction, cancellationToken));
            await InsertCheckedItemsAsync(connection, transaction, id, checkedItems, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return id;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.CommandFailed(exception, nameof(InsertAsync));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ReplaceCheckedItemsAsync(Guid id, IReadOnlyList<string> checkedItems, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(Text(BespokeReportTemplateSql.DeleteCheckedItems, new { Id = id }, transaction, cancellationToken));
            await InsertCheckedItemsAsync(connection, transaction, id, checkedItems, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.CommandFailed(exception, nameof(ReplaceCheckedItemsAsync));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateTitleAsync(Guid id, string title, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(Text(BespokeReportTemplateSql.UpdateTitle, new { Id = id, Title = title }, null, cancellationToken));
        }
        catch (DbException exception)
        {
            logger.CommandFailed(exception, nameof(UpdateTitleAsync));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(Text(BespokeReportTemplateSql.DeleteCheckedItems, new { Id = id }, transaction, cancellationToken));
            await connection.ExecuteAsync(Text(BespokeReportTemplateSql.DeleteTemplate, new { Id = id }, transaction, cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.CommandFailed(exception, nameof(DeleteAsync));
            throw;
        }
    }

    private static CommandDefinition Text(string sql, object? parameters, DbTransaction? transaction, CancellationToken cancellationToken) =>
        new(sql, parameters, transaction, commandType: CommandType.Text, cancellationToken: cancellationToken);

    private static async Task InsertCheckedItemsAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid templateId,
        IReadOnlyList<string> checkedItems,
        CancellationToken cancellationToken)
    {
        if (checkedItems.Count == 0)
        {
            return;
        }

        // Dapper runs the command once for each element of a parameter sequence.
        var rows = checkedItems.Select(checkedId => new { Id = Guid.NewGuid(), TemplateId = templateId, CheckedId = checkedId });

        await connection.ExecuteAsync(Text(BespokeReportTemplateSql.InsertCheckedItem, rows, transaction, cancellationToken));
    }

    private static async Task RollbackAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The transaction was already rolled back by the server; nothing left to undo.
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(BespokeReportTemplateRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);

            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.CommandFailed(exception, "OpenConnection");
            await dbConnection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Row shape of <c>BespokeReportTemplate</c>; column names map by Dapper convention.</summary>
    private sealed record TemplateRow(Guid Id, string? Title);
}
