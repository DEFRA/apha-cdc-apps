using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileQuestions;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileQuestionRepository"/>.
/// </summary>
/// <remarks>
/// <c>spgProfileQuestion</c> returns a result set with more columns than the legacy reader used
/// (and a repeated <c>LastUpdated</c> column read by name), so it is read positionally through a
/// data reader, exactly as <c>SpeciesRepository</c> already does.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileQuestionRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileQuestionRepository> logger)
    : IProfileQuestionRepository
{
    private const int RowVersionLength = 8;

    /// <inheritdoc />
    public async Task<ProfileQuestion?> GetProfileQuestionAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            return await ReadProfileQuestionAsync(connection, transaction: null, id, cancellationToken);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileQuestionStoredProcedures.GetProfileQuestion);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileQuestionInfo>> GetProfileQuestionInfoListAsync(
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ProfileQuestionInfoRow>(new CommandDefinition(
                ProfileQuestionStoredProcedures.GetProfileQuestionBySectionId,
                new { ProfileSectionId = profileSectionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ProfileQuestionInfo
            {
                Id = row.Id,
                Name = row.Name ?? string.Empty,
                QuestionNumber = row.QuestionNumber
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileQuestionStoredProcedures.GetProfileQuestionBySectionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfileQuestion?> UpdateProfileQuestionAsync(UpdateProfileQuestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", command.Id, DbType.Guid);
            parameters.Add("@UserGuidance", command.UserGuidance, DbType.String);
            parameters.Add("@LastUpdated", command.LastUpdated, DbType.Binary, size: RowVersionLength);
            parameters.Add("@Name", command.Name, DbType.String);
            parameters.Add("@NonTechnicalName", command.NonTechnicalName, DbType.String);
            parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileQuestionStoredProcedures.UpdateProfileQuestion,
                parameters,
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // A NULL output means spuProfileQuestion did not find a matching row.
            if (parameters.Get<byte[]?>("@NewLastUpdated") is null)
            {
                await RollbackAsync(transaction, cancellationToken);
                return null;
            }

            var updated = await ReadProfileQuestionAsync(connection, transaction, command.Id, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return updated;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);

            throw SpeciesRepository.IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Profile question '{command.Id}' has been edited by another user. Re-read the question and try again.",
                    exception)
                : exception;
        }
    }

    private static async Task<ProfileQuestion?> ReadProfileQuestionAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                ProfileQuestionStoredProcedures.GetProfileQuestion,
                new { Id = id },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken),
            CommandBehavior.Default);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProfileQuestion
        {
            Id = ReadGuid(reader, 0),
            Name = ReadString(reader, 1),
            ShortName = ReadString(reader, 2),
            QuestionNumber = ReadInt32(reader, 3),
            UserGuidance = ReadString(reader, 4),
            NonTechnicalName = ReadString(reader, 6),
            LastUpdated = ReadRowVersion(reader, reader.GetOrdinal("LastUpdated"))
        };
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
                $"{nameof(ProfileQuestionRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
        }

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            return dbConnection;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, "OpenConnection");
            await dbConnection.DisposeAsync();
            throw;
        }
    }

    private static string ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static Guid ReadGuid(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);

    private static int ReadInt32(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);

    private static byte[] ReadRowVersion(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return [];
        }

        var buffer = new byte[RowVersionLength];
        reader.GetBytes(ordinal, 0, buffer, 0, RowVersionLength);

        return buffer;
    }

    /// <summary>Row shape returned by <c>spgProfileQuestionBySectionId</c>; column names map by Dapper convention.</summary>
    private sealed record ProfileQuestionInfoRow(Guid Id, string? Name, int QuestionNumber);
}
