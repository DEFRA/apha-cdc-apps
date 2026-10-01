using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileNotes;
using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileNoteRepository"/>.
/// </summary>
/// <remarks>
/// The two "get notes" procedures return a note result set followed by a question-reference
/// result set whose columns are only meaningful positionally (the first column repeats the
/// note id as a foreign key), so both are read through a data reader, exactly as
/// <c>SpeciesRepository</c> and <c>ProfileManagementRepository</c> already do.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileNoteRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileNoteRepository> logger)
    : IProfileNoteRepository
{
    private const int RowVersionLength = 8;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteType>> GetNoteTypesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await connection.QueryAsync<ProfileNoteTypeRow>(new CommandDefinition(
                ProfileNoteStoredProcedures.GetNoteTypes,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return [.. rows.Select(row => new ProfileNoteType
            {
                Id = row.Id,
                Name = row.Name ?? string.Empty,
                PluralName = row.PluralName ?? string.Empty
            })];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileNoteStoredProcedures.GetNoteTypes);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNote>> GetNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileNoteStoredProcedures.GetNotesBySectionAndType,
                    new { ProfileNoteTypeId = noteTypeId, ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var notesById = await ReadNotesAsync(reader, cancellationToken);

            // Result set 2: ProfileVersionNoteId, ProfileQuestionId. The section is constant for
            // every row - it is the section requested - exactly as the legacy reader supplied it
            // from the request rather than the result set.
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var noteId = ReadGuid(reader, 0);

                    if (notesById.TryGetValue(noteId, out var note))
                    {
                        note.References.Add(new QuestionReference { ProfileSectionId = profileSectionId, ProfileQuestionId = ReadGuid(reader, 1) });
                    }
                }
            }

            return BuildNotes(notesById);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileNoteStoredProcedures.GetNotesBySectionAndType);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNote>> GetNotesByVersionAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileNoteStoredProcedures.GetNotesByType,
                    new { ProfileNoteTypeId = noteTypeId, ProfileVersionId = profileVersionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var notesById = await ReadNotesAsync(reader, cancellationToken);

            // Result set 2: ProfileVersionNoteId, ProfileSectionId, ProfileQuestionId.
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var noteId = ReadGuid(reader, 0);

                    if (notesById.TryGetValue(noteId, out var note))
                    {
                        note.References.Add(new QuestionReference
                        {
                            ProfileSectionId = ReadGuid(reader, 1),
                            ProfileQuestionId = ReadGuid(reader, 2)
                        });
                    }
                }
            }

            return BuildNotes(notesById);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileNoteStoredProcedures.GetNotesByType);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfileNoteChangesetResult> UpdateNotesAsync(
        UpdateNotesCommand command,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Read which sections are affected before applying any change, exactly as the
            // legacy UpdateNotes did: a delete always affects every section the note was linked
            // to; an update only affects its currently linked sections when the note text is
            // actually changing.
            var affectedSections = await ComputeAffectedSectionsAsync(connection, transaction, command, cancellationToken);

            foreach (var delete in command.Deletes)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileNoteStoredProcedures.DeleteProfileVersionNote,
                    new { delete.Id, delete.LastUpdated },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            var idInsertList = new List<Guid>();
            var lastUpdatedInsertList = new List<byte[]>();

            foreach (var insert in command.Inserts)
            {
                var newLastUpdated = await InsertNoteAsync(connection, transaction, command, insert, cancellationToken);
                idInsertList.Add(insert.Id);
                lastUpdatedInsertList.Add(newLastUpdated);
            }

            var lastUpdatedUpdateList = new List<byte[]>();

            foreach (var update in command.Updates)
            {
                lastUpdatedUpdateList.Add(await UpdateNoteAsync(connection, transaction, update, cancellationToken));
            }

            foreach (var sectionId in affectedSections)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileNoteStoredProcedures.InsertProfileVersionSectionUser,
                    new { UserId = userId, command.ProfileVersionId, ProfileSectionId = sectionId, LastContributionDate = DateTime.Now },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);

            return new ProfileNoteChangesetResult(idInsertList, lastUpdatedInsertList, lastUpdatedUpdateList);
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);

            throw SpeciesRepository.IsConcurrencyViolation(exception)
                ? new ConcurrencyException(
                    $"Profile version '{command.ProfileVersionId}' has a note that has been edited by another user. Re-read the notes and try again.",
                    exception)
                : exception;
        }
    }

    private static async Task<byte[]> InsertNoteAsync(
        DbConnection connection,
        DbTransaction transaction,
        UpdateNotesCommand command,
        ProfileNoteInsertDto insert,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Id", insert.Id, DbType.Guid);
        parameters.Add("@ProfileVersionId", command.ProfileVersionId, DbType.Guid);
        parameters.Add("@ProfileNoteTypeId", command.NoteTypeId, DbType.Guid);
        parameters.Add("@NoteText", insert.NoteText, DbType.String);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileNoteStoredProcedures.InsertProfileVersionNote,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        foreach (var reference in insert.QuestionReferenceAdds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileNoteStoredProcedures.InsertProfileVersionNoteQuestion,
                new { ProfileVersionNoteId = insert.Id, reference.ProfileQuestionId, reference.ProfileSectionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }

        return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
    }

    private static async Task<byte[]> UpdateNoteAsync(
        DbConnection connection,
        DbTransaction transaction,
        ProfileNoteUpdateDto update,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Id", update.Id, DbType.Guid);
        parameters.Add("@NoteText", update.NoteText, DbType.String);
        parameters.Add("@LastUpdated", update.LastUpdated, DbType.Binary, size: RowVersionLength);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileNoteStoredProcedures.UpdateProfileVersionNote,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        foreach (var reference in update.QuestionReferenceAdds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileNoteStoredProcedures.InsertProfileVersionNoteQuestion,
                new { ProfileVersionNoteId = update.Id, reference.ProfileQuestionId, reference.ProfileSectionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }

        foreach (var reference in update.QuestionReferenceRemoves)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileNoteStoredProcedures.DeleteProfileVersionNoteQuestion,
                new { ProfileVersionNoteId = update.Id, reference.ProfileQuestionId, reference.ProfileSectionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }

        return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
    }

    /// <summary>Mirrors the legacy <c>ProfileNoteService.GetSectionList</c>.</summary>
    private static async Task<List<Guid>> ComputeAffectedSectionsAsync(
        DbConnection connection,
        DbTransaction transaction,
        UpdateNotesCommand command,
        CancellationToken cancellationToken)
    {
        var sectionIds = new List<Guid>();

        foreach (var delete in command.Deletes)
        {
            await AddLinkedSectionsAsync(connection, transaction, delete.Id, isDelete: true, newNoteText: string.Empty, sectionIds, cancellationToken);
        }

        foreach (var update in command.Updates)
        {
            await AddLinkedSectionsAsync(connection, transaction, update.Id, isDelete: false, newNoteText: update.NoteText, sectionIds, cancellationToken);

            AddSectionIds(sectionIds, update.QuestionReferenceAdds.Select(reference => reference.ProfileSectionId));
            AddSectionIds(sectionIds, update.QuestionReferenceRemoves.Select(reference => reference.ProfileSectionId));
        }

        foreach (var insert in command.Inserts)
        {
            AddSectionIds(sectionIds, insert.QuestionReferenceAdds.Select(reference => reference.ProfileSectionId));
        }

        return sectionIds;
    }

    private static async Task AddLinkedSectionsAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionNoteId,
        bool isDelete,
        string newNoteText,
        List<Guid> sectionIds,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                ProfileNoteStoredProcedures.GetProfileSectionIdByNoteId,
                new { ProfileVersionNoteId = profileVersionNoteId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken),
            CommandBehavior.Default);

        while (await reader.ReadAsync(cancellationToken))
        {
            var sectionId = ReadGuid(reader, 0);
            var oldNoteText = ReadString(reader, 1);

            // A delete always counts every currently linked section; an update only counts a
            // section when the note text is actually changing, exactly as the legacy
            // GetSectionListByProfileVersionNoteId did.
            if (isDelete || !string.Equals(oldNoteText, newNoteText, StringComparison.Ordinal))
            {
                AddSectionId(sectionIds, sectionId);
            }
        }
    }

    private static void AddSectionId(List<Guid> sectionIds, Guid sectionId)
    {
        if (!sectionIds.Contains(sectionId))
        {
            sectionIds.Add(sectionId);
        }
    }

    private static void AddSectionIds(List<Guid> sectionIds, IEnumerable<Guid> candidates)
    {
        foreach (var sectionId in candidates)
        {
            AddSectionId(sectionIds, sectionId);
        }
    }

    private static async Task<Dictionary<Guid, NoteState>> ReadNotesAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var notesById = new Dictionary<Guid, NoteState>();
        var lastUpdatedOrdinal = reader.GetOrdinal("LastUpdated");

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = ReadGuid(reader, 0);
            notesById[id] = new NoteState(ReadString(reader, 1), ReadRowVersion(reader, lastUpdatedOrdinal), []);
        }

        return notesById;
    }

    private static IReadOnlyList<ProfileNote> BuildNotes(Dictionary<Guid, NoteState> notesById) =>
        [.. notesById.Select(entry => new ProfileNote
        {
            Id = entry.Key,
            NoteText = entry.Value.NoteText,
            LastUpdated = entry.Value.LastUpdated,
            QuestionReferences = entry.Value.References
        })];

    /// <summary>A note's text and row version, plus the question references accumulated for it while reading result set 2.</summary>
    private sealed record NoteState(string NoteText, byte[] LastUpdated, List<QuestionReference> References);

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
                $"{nameof(ProfileNoteRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    /// <summary>Row shape returned by <c>spgaProfileNoteType</c>; column names map by Dapper convention.</summary>
    private sealed record ProfileNoteTypeRow(Guid Id, string? Name, string? PluralName);
}
