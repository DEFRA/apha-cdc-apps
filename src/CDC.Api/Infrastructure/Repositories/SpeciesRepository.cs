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

    private const int RowVersionLength = 8;

    /// <summary><c>spuSpecies</c> declares <c>@Name varchar(50)</c>.</summary>
    private const int SpeciesNameMaxLength = 50;

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

            var sections = await ReadSectionMetadataAsync(reader, cancellationToken);
            var questions = await ReadQuestionMetadataAsync(reader, cancellationToken);
            var fields = await ReadFieldMetadataAsync(reader, cancellationToken);

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

            var lastUpdated = ReadRowVersion(reader, 0);
            var speciesName = ReadString(reader, 1);

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
                        BooleanValue = ReadNullableBoolean(reader, 2),
                        ListValue = ReadNullableGuid(reader, 3),
                        TextValue = ReadNullableString(reader, 4),
                        QuestionId = reader.GetGuid(5),
                        FieldNumber = ReadInt32(reader, 6)
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
        parameters.Add("@Reason", command.Reason, DbType.AnsiString, size: 255);
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
    public async Task<IReadOnlyList<Domain.Entities.SpeciesAuditTrailEntry>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken)
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
                .Select(row => new Domain.Entities.SpeciesAuditTrailEntry
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

    private static async Task<List<SectionMetadataRow>> ReadSectionMetadataAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var sections = new List<SectionMetadataRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            sections.Add(new SectionMetadataRow(
                reader.GetGuid(0),
                ReadString(reader, 1),
                ReadString(reader, 2),
                ReadInt32(reader, 3)));
        }

        return sections;
    }

    private static async Task<List<QuestionMetadataRow>> ReadQuestionMetadataAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var questions = new List<QuestionMetadataRow>();

        if (!await reader.NextResultAsync(cancellationToken))
        {
            return questions;
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            questions.Add(new QuestionMetadataRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                ReadString(reader, 2),
                ReadInt32(reader, 3),
                ReadString(reader, 4)));
        }

        return questions;
    }

    private static async Task<List<FieldMetadataRow>> ReadFieldMetadataAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        const int editorFieldTypeOrdinal = 11;
        var fields = new List<FieldMetadataRow>();

        if (!await reader.NextResultAsync(cancellationToken))
        {
            return fields;
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            fields.Add(new FieldMetadataRow(
                reader.GetGuid(1),
                reader.GetGuid(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadInt32(reader, 5),
                ReadGuid(reader, 6),
                ReadString(reader, 7),
                ReadBoolean(reader, 8),
                ReadGuid(reader, 9),
                ReadBoolean(reader, 10),
                // Databases that predate the editor field type column simply omit it.
                reader.FieldCount > editorFieldTypeOrdinal ? ReadInt32(reader, editorFieldTypeOrdinal) : 0));
        }

        return fields;
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

    private static byte[] ReadRowVersion(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? [] : (byte[])reader.GetValue(ordinal);

    private static string ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static string? ReadNullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static Guid ReadGuid(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);

    private static Guid? ReadNullableGuid(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    private static bool ReadBoolean(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);

    private static bool? ReadNullableBoolean(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);

    private static int ReadInt32(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);

    // Property-initialised (not positional) so Dapper binds columns by name rather than by
    // ordinal position - the actual stored procedure's column order is not guaranteed to match
    // declaration order here.
    private sealed record SpeciesRow
    {
        public Guid Id { get; init; }
        public Guid? ParentId { get; init; }
        public string? Name { get; init; }
        public bool IsActive { get; init; }
        public bool IsInUse { get; init; }
    }

    private sealed record SelectedSpeciesRow
    {
        public Guid Id { get; init; }
        public Guid? ParentId { get; init; }
        public string? Name { get; init; }
        public bool IsActive { get; init; }
        public bool IsInUse { get; init; }
        public string? DiseaseName { get; init; }
        public int Disease1 { get; init; }
        public int Disease2 { get; init; }
        public int Disease3 { get; init; }
        public int Disease4 { get; init; }
        public string? Disease5 { get; init; }
        public long FilterNumber { get; init; }
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
        public string? Name { get; init; }
        public Guid? ParentId { get; init; }
        public bool IsActive { get; init; }
        public bool IsInUse { get; init; }
        public int ChildCount { get; init; }
        public int ActiveChildCount { get; init; }
        public string? ParentName { get; init; }
        public byte[]? LastUpdated { get; init; }
    }

    private sealed record SpeciesValidParentRow
    {
        public Guid Id { get; init; }
        public string? Name { get; init; }
    }

    private sealed record SpeciesAuditTrailRow
    {
        public Guid Id { get; init; }
        public string? FullName { get; init; }
        public DateTime LogDate { get; init; }
        public string? Reason { get; init; }
        public string? OldName { get; init; }
        public string? NewName { get; init; }
        public string? OldParent { get; init; }
        public string? NewParent { get; init; }
    }
}
