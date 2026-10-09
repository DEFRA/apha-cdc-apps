using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species;
using CDC.Api.Features.Species.Commands;
using CDC.Api.Features.Species.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="ISpeciesRepository"/>.
/// </summary>
/// <remarks>
/// Two of the stored procedures return result sets containing duplicate column names
/// (<c>Id</c> appears twice) and one unnamed column, so those are read positionally through a
/// data reader exactly as the legacy VB.NET layer did. The remaining procedures return
/// uniquely named columns and are mapped by Dapper.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SpeciesRepository(IDbConnectionFactory connectionFactory, ILogger<SpeciesRepository> logger)
    : ISpeciesRepository
{
    /// <summary>SQL Server reports <c>RAISERROR</c> with a user-defined message as error 50000.</summary>
    private const int UserRaisedErrorNumber = 50000;

    /// <summary>The message <c>spuSpeciesAnswerData</c> raises when the row version has moved on.</summary>
    private const string ConcurrencyMessageFragment = "edited by another user";

    /// <summary>The message <c>spiSpecies</c> and <c>spuSpecies</c> raise for a name clash.</summary>
    private const string DuplicateNameMessageFragment = "already a species with this name";

    /// <summary>The message <c>spuSpeciesSequenceNumber</c> raises when no sibling exists at the resulting sequence number.</summary>
    private const string ReorderBlockedMessageFragment = "no species above/below this one";

    private const int RowVersionLength = 8;

    /// <summary><c>spuSpecies</c> declares <c>@Name varchar(50)</c>.</summary>
    private const int SpeciesNameMaxLength = 50;

    /// <summary><c>spuSpecies</c> declares <c>@Reason varchar(255)</c>.</summary>
    private const int ReasonMaxLength = 255;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Species>> GetAllSpeciesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<SpeciesRow>(new CommandDefinition(
                SpeciesStoredProcedures.GetAllSpecies,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new Domain.Entities.Species
            {
                Id = row.Id,
                ParentId = row.ParentId ?? Guid.Empty,
                Description = row.Name ?? string.Empty,
                IsActive = row.IsActive,
                IsInUse = row.IsInUse
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetAllSpecies);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SelectedSpecies>> GetAllSelectedSpeciesAsync(
        string diseaseName,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<SelectedSpeciesRow>(new CommandDefinition(
                SpeciesStoredProcedures.GetAllSelectedSpecies,
                new { DiseaseName = diseaseName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new SelectedSpecies
            {
                Id = row.Id,
                ParentId = row.ParentId ?? Guid.Empty,
                Description = row.Name ?? string.Empty,
                IsActive = row.IsActive,
                IsInUse = row.IsInUse,
                DiseaseName = row.DiseaseName ?? string.Empty,
                Disease1 = row.Disease1,
                Disease2 = row.Disease2,
                Disease3 = row.Disease3,
                Disease4 = row.Disease4,
                Disease5 = row.Disease5 ?? string.Empty,
                FilterNumber = row.FilterNumber
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetAllSelectedSpecies);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SpeciesMetadata> GetSpeciesMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    SpeciesStoredProcedures.GetSpeciesMetadata,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var sections = await MetadataResultSetReader.ReadCurrentResultSetAsync(
                reader,
                static r => new SectionMetadataRow(r.GetGuid(0), r.ReadString(1), r.ReadString(2), r.ReadInt32(3)),
                cancellationToken);

            var questions = await MetadataResultSetReader.ReadNextResultSetAsync(
                reader,
                static r => new QuestionMetadataRow(r.GetGuid(0), r.GetGuid(1), r.ReadString(2), r.ReadInt32(3), r.ReadString(4)),
                cancellationToken);

            var fields = await MetadataResultSetReader.ReadNextResultSetAsync(
                reader,
                static r => MapFieldMetadataRow(r),
                cancellationToken);

            return BuildMetadata(sections, questions, fields);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetSpeciesMetadata);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SpeciesAnswerData?> GetSpeciesAnswerDataAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    SpeciesStoredProcedures.GetSpeciesAnswerData,
                    new { SpeciesId = speciesId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Result set 1: LastUpdated, Name, SpeciesId. No row means no such species.
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var lastUpdated = reader.ReadRowVersion(0);
            var speciesName = reader.ReadString(1);

            // Result set 2: every section id, in section order.
            var sectionIds = new List<Guid>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    sectionIds.Add(reader.GetGuid(0));
                }
            }

            var valuesBySection = sectionIds.ToDictionary(id => id, _ => new List<SpeciesFieldValue>());

            // Result set 3: SpeciesSectionId, Id, BooleanValue, ListValue, TextValue, QuestionId, FieldNumber.
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sectionId = reader.GetGuid(0);

                    // A value whose section is not in result set 2 cannot be displayed, so skip
                    // it rather than fail the whole request.
                    if (!valuesBySection.TryGetValue(sectionId, out var values))
                    {
                        continue;
                    }

                    values.Add(new SpeciesFieldValue
                    {
                        Id = reader.GetGuid(1),
                        BooleanValue = reader.ReadNullableBoolean(2),
                        ListValue = reader.ReadNullableGuid(3),
                        TextValue = reader.ReadNullableString(4),
                        QuestionId = reader.GetGuid(5),
                        FieldNumber = reader.ReadInt32(6)
                    });
                }
            }

            return new SpeciesAnswerData
            {
                SpeciesId = speciesId,
                SpeciesName = speciesName,
                LastUpdated = lastUpdated,
                Sections = [.. sectionIds.Select(id => new SpeciesSection
                {
                    SectionId = id,
                    FieldValues = valuesBySection[id]
                })]
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetSpeciesAnswerData);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> UpdateSpeciesAnswerDataAsync(
        UpdateSpeciesAnswerDataCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var lastUpdated = await UpdateRowVersionAsync(connection, transaction, command, cancellationToken);

            foreach (var change in command.Changes)
            {
                if (change.Kind == SpeciesFieldValueKind.MultiValue)
                {
                    await ReplaceMultiValueAsync(connection, transaction, command.SpeciesId, change, cancellationToken);
                }
                else
                {
                    await UpdateFieldValueAsync(connection, transaction, command.SpeciesId, change, cancellationToken);
                }
            }

            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.CalculatePrioritisationScore,
                transaction: transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return lastUpdated;
        }
        catch (DbException exception)
        {
            // Disposing an uncommitted transaction rolls it back, but rolling back explicitly
            // releases the locks immediately and makes the failure path unambiguous.
            await RollbackAsync(transaction, cancellationToken);
            logger.UpdateRolledBack(exception, command.SpeciesId);

            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Species '{command.SpeciesId}' has been edited by another user. Re-read the answer data and try again.",
                    exception)
                : exception;
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(transaction, CancellationToken.None);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Domain.Entities.SpeciesDetail?> GetSpeciesByIdAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<SpeciesDetailRow>(new CommandDefinition(
                SpeciesStoredProcedures.GetSpeciesById,
                new { SpeciesId = speciesId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            if (row is null)
            {
                return null;
            }

            return new Domain.Entities.SpeciesDetail
            {
                Id = speciesId,
                Name = row.Name ?? string.Empty,
                ParentId = row.ParentId ?? Guid.Empty,
                ParentName = row.ParentName ?? string.Empty,
                IsActive = row.IsActive,
                IsInUse = row.IsInUse,
                ChildCount = row.ChildCount,
                ActiveChildCount = row.ActiveChildCount,
                LastUpdated = row.LastUpdated ?? []
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetSpeciesById);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesValidParent>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<SpeciesValidParentRow>(new CommandDefinition(
                SpeciesStoredProcedures.GetSpeciesValidParents,
                new { SpeciesId = speciesId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new SpeciesValidParent
            {
                Id = row.Id,
                Name = row.Name ?? string.Empty
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetSpeciesValidParents);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        // spuSpecies takes exactly these six parameters and returns nothing: it raises an
        // error (caught below) for a duplicate name or a stale @LastUpdated, otherwise it
        // updates the row and inserts the audit entry itself.
        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", command.SpeciesId, DbType.Guid);
        parameters.Add("@UserId", command.UserId, DbType.Guid);
        parameters.Add("@Reason", command.Reason, DbType.AnsiString, size: ReasonMaxLength);
        parameters.Add("@ParentId", command.ParentId == Guid.Empty ? null : command.ParentId, DbType.Guid);
        parameters.Add("@Name", command.Name, DbType.AnsiString, size: SpeciesNameMaxLength);
        parameters.Add("@LastUpdated", command.LastUpdated, DbType.Binary, size: RowVersionLength);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.UpdateSpecies,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.UpdateSpecies);

            // spuSpecies RAISERRORs (error 50000) for both a duplicate name and a stale row
            // version; its own message text already says which, so it is passed straight
            // through rather than replaced with a generic one.
            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(exception.Message, exception)
                : exception;
        }

        // The procedure has no output parameter for the new row version, so it is re-read.
        var updated = await connection.QuerySingleOrDefaultAsync<SpeciesDetailRow>(new CommandDefinition(
            SpeciesStoredProcedures.GetSpeciesById,
            new { SpeciesId = command.SpeciesId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return updated?.LastUpdated ?? [];
    }

    /// <inheritdoc />
    public async Task<Guid> AddSpeciesAsync(AddSpeciesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The legacy CSLA business object allocated the key before calling the procedure, and
        // spiSpecies still takes it as an input parameter rather than generating one.
        var speciesId = Guid.NewGuid();

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", speciesId, DbType.Guid);
        parameters.Add("@UserId", command.UserId, DbType.Guid);
        parameters.Add("@Reason", command.Reason, DbType.AnsiString, size: ReasonMaxLength);
        // Guid.Empty means "root species", which spiSpecies expects as a NULL parent.
        parameters.Add("@ParentId", command.ParentId is null || command.ParentId == Guid.Empty ? null : command.ParentId, DbType.Guid);
        parameters.Add("@Name", command.Name, DbType.AnsiString, size: SpeciesNameMaxLength);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.InsertSpecies,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.InsertSpecies);

            // spiSpecies RAISERRORs when the name is taken; its own message already says so,
            // so it is passed through rather than replaced with a generic one.
            throw IsDuplicateNameViolation(exception)
                ? new DuplicateSpeciesNameException(exception.Message, exception)
                : exception;
        }

        return speciesId;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SpeciesAuditTrailEntry>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<SpeciesAuditTrailRow>(new CommandDefinition(
                SpeciesStoredProcedures.GetSpeciesAuditTrail,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows
                .OrderByDescending(row => row.LogDate)
                .Select(row => new SpeciesAuditTrailEntry
                {
                    Id = row.Id,
                    OldName = row.OldName ?? string.Empty,
                    NewName = row.NewName ?? string.Empty,
                    OldParent = row.OldParent ?? string.Empty,
                    NewParent = row.NewParent ?? string.Empty,
                    ChangedBy = row.FullName ?? string.Empty,
                    LogDate = row.LogDate,
                    ReasonForChange = row.Reason ?? string.Empty
                })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.GetSpeciesAuditTrail);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ChangeSpeciesPositionAsync(Guid speciesId, bool isMovingUp, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", speciesId, DbType.Guid);
        parameters.Add("@IsMovingUp", isMovingUp, DbType.Boolean);
        parameters.Add("@UserId", userId, DbType.Guid);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.ChangeSpeciesPosition,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.ChangeSpeciesPosition);

            // spuSpeciesSequenceNumber RAISERRORs (error 50000) when no sibling exists at the
            // resulting sequence number - either the species is already first/last, or (as with
            // legacy data) the adjacent sequence number has a gap. The legacy CSLA business
            // object surfaced this inline rather than as a crash, so it is passed through here
            // rather than left to bubble up as an unhandled 500.
            throw IsReorderBlockedViolation(exception)
                ? new SpeciesReorderBlockedException(exception.Message, exception)
                : exception;
        }
    }

    /// <inheritdoc />
    public async Task InactivateSpeciesAsync(InactivateSpeciesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        // sppSpecies takes exactly these four parameters and returns nothing: it raises an
        // error (caught below) for a stale @LastUpdated, otherwise it sets EffectiveDateTo and
        // inserts the audit entry itself.
        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", command.SpeciesId, DbType.Guid);
        parameters.Add("@UserId", command.UserId, DbType.Guid);
        parameters.Add("@Reason", command.Reason, DbType.AnsiString, size: ReasonMaxLength);
        parameters.Add("@LastUpdated", command.LastUpdated, DbType.Binary, size: RowVersionLength);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.InactivateSpecies,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.InactivateSpecies);

            // sppSpecies RAISERRORs (error 50000) only for a stale row version, so this is
            // always a concurrency conflict, unlike spuSpecies which also covers duplicate names.
            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(exception.Message, exception)
                : exception;
        }
    }

    /// <inheritdoc />
    public async Task DeleteSpeciesAsync(DeleteSpeciesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        // spdSpecies takes exactly these four parameters and returns nothing: it raises an
        // error (caught below) for a stale @LastUpdated, otherwise it writes the audit entry,
        // decrements later siblings' sequence numbers, and deletes the species' rows itself.
        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", command.SpeciesId, DbType.Guid);
        parameters.Add("@UserId", command.UserId, DbType.Guid);
        parameters.Add("@Reason", command.Reason, DbType.AnsiString, size: ReasonMaxLength);
        parameters.Add("@LastUpdated", command.LastUpdated, DbType.Binary, size: RowVersionLength);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.DeleteSpecies,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, SpeciesStoredProcedures.DeleteSpecies);

            // spdSpecies RAISERRORs (error 50000) only for a stale row version, so this is
            // always a concurrency conflict, unlike spuSpecies which also covers duplicate names.
            throw IsConcurrencyViolation(exception)
                ? new ConcurrencyException(exception.Message, exception)
                : exception;
        }
    }

    /// <summary>
    /// Identifies the row version clash raised by <c>spuSpeciesAnswerData</c>. The message is
    /// also matched so that providers other than SQL Server - including the fakes used in
    /// tests - can signal the same condition.
    /// </summary>
    /// <param name="exception">The database exception to classify.</param>
    /// <returns><see langword="true"/> when the failure is a concurrent edit.</returns>
    internal static bool IsConcurrencyViolation(DbException exception) =>
        exception is SqlException { Number: UserRaisedErrorNumber } ||
        exception.Message.Contains(ConcurrencyMessageFragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Identifies the name clash raised by <c>spiSpecies</c>. Matched on the message because
    /// it shares error number 50000 with every other <c>RAISERROR</c> the procedure can emit.
    /// </summary>
    /// <param name="exception">The database exception to classify.</param>
    /// <returns><see langword="true"/> when the failure is a duplicate species name.</returns>
    internal static bool IsDuplicateNameViolation(DbException exception) =>
        exception.Message.Contains(DuplicateNameMessageFragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Identifies the "no sibling at that position" failure raised by
    /// <c>spuSpeciesSequenceNumber</c>. Matched on the message because it shares error number
    /// 50000 with every other <c>RAISERROR</c> the procedure can emit.
    /// </summary>
    /// <param name="exception">The database exception to classify.</param>
    /// <returns><see langword="true"/> when the move was rejected because no sibling exists there.</returns>
    internal static bool IsReorderBlockedViolation(DbException exception) =>
        exception.Message.Contains(ReorderBlockedMessageFragment, StringComparison.OrdinalIgnoreCase);

    private static async Task<byte[]> UpdateRowVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        UpdateSpeciesAnswerDataCommand command,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", command.SpeciesId, DbType.Guid);
        parameters.Add("@LastUpdated", command.LastUpdated, DbType.Binary, size: RowVersionLength);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

        await connection.ExecuteAsync(new CommandDefinition(
            SpeciesStoredProcedures.UpdateSpeciesAnswerData,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
    }

    private static async Task UpdateFieldValueAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid speciesId,
        SpeciesFieldValueChange change,
        CancellationToken cancellationToken)
    {
        // The stored procedure defaults every value parameter to NULL and deletes the row when
        // all three are NULL, which is how a "None" change clears an answer.
        var parameters = new DynamicParameters();
        parameters.Add("@SpeciesId", speciesId, DbType.Guid);
        parameters.Add("@SpeciesFieldId", change.FieldId, DbType.Guid);

        switch (change.Kind)
        {
            case SpeciesFieldValueKind.Boolean:
                parameters.Add("@BooleanValue", change.BooleanValue, DbType.Boolean);
                break;
            case SpeciesFieldValueKind.List:
                parameters.Add("@ListValue", change.ListValue, DbType.Guid);
                break;
            case SpeciesFieldValueKind.Text:
                parameters.Add("@TextValue", change.TextValue, DbType.AnsiString);
                break;
            case SpeciesFieldValueKind.None:
            case SpeciesFieldValueKind.MultiValue:
            default:
                break;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            SpeciesStoredProcedures.UpdateSpeciesFieldValue,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplaceMultiValueAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid speciesId,
        SpeciesFieldValueChange change,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            SpeciesStoredProcedures.DeleteSpeciesFieldMultiValue,
            new { SpeciesId = speciesId, SpeciesFieldId = change.FieldId },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        foreach (var listValue in change.MultiValues)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                SpeciesStoredProcedures.InsertSpeciesFieldMultiValue,
                new { SpeciesId = speciesId, SpeciesFieldId = change.FieldId, ListValue = listValue },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
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

    private static FieldMetadataRow MapFieldMetadataRow(DbDataReader reader)
    {
        const int editorFieldTypeOrdinal = 11;

        return new FieldMetadataRow(
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.ReadString(3),
            reader.ReadString(4),
            reader.ReadInt32(5),
            reader.ReadGuid(6),
            reader.ReadString(7),
            reader.ReadBoolean(8),
            reader.ReadGuid(9),
            reader.ReadBoolean(10),
            // Databases that predate the editor field type column simply omit it.
            reader.FieldCount > editorFieldTypeOrdinal ? reader.ReadInt32(editorFieldTypeOrdinal) : 0);
    }

    private static SpeciesMetadata BuildMetadata(
        List<SectionMetadataRow> sections,
        List<QuestionMetadataRow> questions,
        List<FieldMetadataRow> fields)
    {
        var fieldsByQuestion = fields
            .GroupBy(field => field.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var questionsBySection = questions
            .GroupBy(question => question.SectionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return new SpeciesMetadata
        {
            Sections = [.. sections.Select(section => new SpeciesSectionMetadata
            {
                Id = section.Id,
                Name = section.Name,
                ShortName = section.ShortName,
                SectionNumber = section.SectionNumber,
                Questions = questionsBySection.TryGetValue(section.Id, out var sectionQuestions)
                    ? [.. sectionQuestions.Select(question => new SpeciesQuestionMetadata
                    {
                        Id = question.Id,
                        SectionId = question.SectionId,
                        Name = question.Name,
                        ShortName = question.ShortName,
                        QuestionNumber = question.QuestionNumber,
                        Fields = fieldsByQuestion.TryGetValue(question.Id, out var questionFields)
                            ? [.. questionFields.Select(field => new SpeciesFieldMetadata
                            {
                                Id = field.Id,
                                QuestionId = field.QuestionId,
                                Name = field.Name,
                                ShortName = field.ShortName,
                                FieldNumber = field.FieldNumber,
                                DataFieldTypeId = field.DataFieldTypeId,
                                DataTypeName = field.DataTypeName,
                                IsMandatory = field.IsMandatory,
                                ReferenceTableId = field.ReferenceTableId,
                                ReferenceTableIsMaintainable = field.ReferenceTableIsMaintainable,
                                EditorFieldType = field.EditorFieldType
                            })]
                            : []
                    })]
                    : []
            })]
        };
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(SpeciesRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    // Property-initialised (not positional) so Dapper binds columns by name rather than by
    // ordinal position - the actual stored procedure's column order is not guaranteed to match
    // declaration order here. Dapper sets these via reflection, which static analysis can't see,
    // so the init accessors below are intentionally kept despite looking "unused".
    private sealed record SpeciesRow
    {
        public Guid Id { get; init; } // NOSONAR
        public Guid? ParentId { get; init; } // NOSONAR
        public string? Name { get; init; } // NOSONAR
        public bool IsActive { get; init; } // NOSONAR
        public bool IsInUse { get; init; } // NOSONAR
    }

    private sealed record SelectedSpeciesRow
    {
        public Guid Id { get; init; } // NOSONAR
        public Guid? ParentId { get; init; } // NOSONAR
        public string? Name { get; init; } // NOSONAR
        public bool IsActive { get; init; } // NOSONAR
        public bool IsInUse { get; init; } // NOSONAR
        public string? DiseaseName { get; init; } // NOSONAR
        public int Disease1 { get; init; } // NOSONAR
        public int Disease2 { get; init; } // NOSONAR
        public int Disease3 { get; init; } // NOSONAR
        public int Disease4 { get; init; } // NOSONAR
        public string? Disease5 { get; init; } // NOSONAR
        public long FilterNumber { get; init; } // NOSONAR
    }

    private sealed record SectionMetadataRow(Guid Id, string Name, string ShortName, int SectionNumber);

    private sealed record QuestionMetadataRow(Guid SectionId, Guid Id, string Name, int QuestionNumber, string ShortName);

    private sealed record FieldMetadataRow(
        Guid QuestionId,
        Guid Id,
        string Name,
        string ShortName,
        int FieldNumber,
        Guid DataFieldTypeId,
        string DataTypeName,
        bool IsMandatory,
        Guid ReferenceTableId,
        bool ReferenceTableIsMaintainable,
        int EditorFieldType);

    private sealed record SpeciesDetailRow
    {
        public string? Name { get; init; } // NOSONAR
        public Guid? ParentId { get; init; } // NOSONAR
        public bool IsActive { get; init; } // NOSONAR
        public bool IsInUse { get; init; } // NOSONAR
        public int ChildCount { get; init; } // NOSONAR
        public int ActiveChildCount { get; init; } // NOSONAR
        public string? ParentName { get; init; } // NOSONAR
        public byte[]? LastUpdated { get; init; } // NOSONAR
    }

    private sealed record SpeciesValidParentRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? Name { get; init; } // NOSONAR
    }

    private sealed record SpeciesAuditTrailRow
    {
        public Guid Id { get; init; } // NOSONAR
        public string? FullName { get; init; } // NOSONAR
        public DateTime LogDate { get; init; } // NOSONAR
        public string? Reason { get; init; } // NOSONAR
        public string? OldName { get; init; } // NOSONAR
        public string? NewName { get; init; } // NOSONAR
        public string? OldParent { get; init; } // NOSONAR
        public string? NewParent { get; init; } // NOSONAR
    }
}
