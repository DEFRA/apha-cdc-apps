using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections;
using CDC.Api.Features.ProfileSections.Interfaces;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileSectionRepository"/>.
/// </summary>
/// <remarks>
/// Both stored procedures return several result sets, so they are read positionally through a
/// data reader exactly as <see cref="SpeciesRepository"/> already does for the equivalent
/// species questionnaire procedures. Result sets not needed for read-only browsing (profiled
/// species, revision dates, repeating question rows) are skipped via <c>NextResultAsync</c>.
/// </remarks>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileSectionRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileSectionRepository> logger)
    : IProfileSectionRepository
{
    private const int RowVersionLength = 8;

    /// <inheritdoc />
    public async Task<ProfileQuestionnaireMetadata> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileSectionStoredProcedures.GetProfileQuestionnaireMetadata,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var sections = await MetadataResultSetReader.ReadCurrentResultSetAsync(
                reader,
                static r => new SectionMetadataRow(r.GetGuid(0), r.ReadString(1), r.ReadString(2), r.ReadInt32(3)),
                cancellationToken);

            var questions = await MetadataResultSetReader.ReadNextResultSetAsync(
                reader,
                static r => new QuestionMetadataRow(r.GetGuid(0), r.GetGuid(1), r.ReadString(2), r.ReadInt32(3), r.ReadBoolean(4), r.ReadBoolean(5)),
                cancellationToken);

            var fields = await MetadataResultSetReader.ReadNextResultSetAsync(
                reader,
                static r => new FieldMetadataRow(
                    r.GetGuid(1),
                    r.GetGuid(2),
                    r.ReadString(3),
                    r.ReadInt32(4),
                    r.ReadGuid(5),
                    DataFieldTypeNames.ToContractName(r.ReadString(6)),
                    r.ReadBoolean(7),
                    r.ReadGuid(8),
                    r.ReadBoolean(9),
                    r.ReadString(11),
                    r.ReadBoolean(12),
                    r.ReadBoolean(13)),
                cancellationToken);

            return BuildMetadata(sections, questions, fields);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileQuestionnaireMetadata);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfileSectionAnswers> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileSectionStoredProcedures.GetProfileVersionSection,
                    new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Result set 1: profiled species - not needed for read-only browsing.
            // Result set 2: ProfileSectionId, ..., LastUpdated (column 7), ... - the row version a later save is checked against.
            await reader.NextResultAsync(cancellationToken);
            var lastUpdated = await reader.ReadAsync(cancellationToken) ? reader.ReadRowVersion(7) : [];
            await reader.NextResultAsync(cancellationToken);

            // Result set 3: Id, Name, NonTechnicalName.
            var questionNames = new List<ProfileQuestionName>();
            while (await reader.ReadAsync(cancellationToken))
            {
                questionNames.Add(new ProfileQuestionName
                {
                    Id = reader.GetGuid(0),
                    Name = reader.ReadString(1),
                    NonTechnicalName = reader.ReadString(2)
                });
            }

            // Result set 4: Id, ProfileQuestionId, SequenceNumber - repeating question rows.
            var questionRows = new List<ProfileQuestionRow>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    questionRows.Add(new ProfileQuestionRow
                    {
                        Id = reader.GetGuid(0),
                        QuestionId = reader.GetGuid(1),
                        SequenceNumber = reader.ReadInt32(2)
                    });
                }
            }

            // Result set 5: Id, SpeciesId, RowId, BooleanValue, ListValue, DecimalValue, DateValue,
            // TextValue, ProfileFieldId, ProfileFieldGroupId, QuestionNumber, QuestionId, FieldNumber.
            var fieldValues = new List<ProfileFieldValue>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    fieldValues.Add(new ProfileFieldValue
                    {
                        Id = reader.GetGuid(0),
                        QuestionId = reader.GetGuid(11),
                        FieldNumber = reader.ReadInt32(12),
                        SpeciesId = reader.ReadNullableGuid(1),
                        ProfileVersionQuestionRowId = reader.ReadNullableGuid(2),
                        BooleanValue = reader.ReadNullableBoolean(3),
                        ListValue = reader.ReadNullableGuid(4),
                        DecimalValue = reader.ReadNullableDecimal(5),
                        DateValue = reader.ReadNullableDateTime(6),
                        TextValue = reader.ReadNullableString(7)
                    });
                }
            }

            return new ProfileSectionAnswers
            {
                ProfileVersionId = profileVersionId,
                ProfileSectionId = profileSectionId,
                QuestionNames = questionNames,
                QuestionRows = questionRows,
                FieldValues = fieldValues,
                LastUpdated = lastUpdated
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileVersionSection);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ApplyProfileVersionSectionChangeAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        ProfileSectionChange change,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var storedProcedure = ProfileSectionStoredProcedures.UpdateProfileVersionSection;

        try
        {
            await WriteSectionRowAsync(connection, transaction, profileVersionId, profileSectionId, change, cancellationToken);

            storedProcedure = ProfileSectionStoredProcedures.DeleteProfileVersionQuestionRow;

            foreach (var rowId in change.QuestionRowIdsToDelete)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    storedProcedure,
                    new { Id = rowId },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            storedProcedure = ProfileSectionStoredProcedures.ClearProfileVersionFieldValue;

            foreach (var instruction in change.Clears)
            {
                await ClearValueAsync(connection, transaction, profileVersionId, instruction, cancellationToken);
            }

            storedProcedure = ProfileSectionStoredProcedures.UpsertProfileVersionFieldValue;

            foreach (var instruction in change.Saves)
            {
                await SaveValueAsync(connection, transaction, profileVersionId, instruction, cancellationToken);
            }

            if (change.ValueChangeCount > 0)
            {
                // Matches the legacy LogUserContribution call, which only fires when at least one
                // field value actually changed.
                storedProcedure = ProfileSectionStoredProcedures.LogProfileVersionSectionContribution;

                await connection.ExecuteAsync(new CommandDefinition(
                    storedProcedure,
                    new { UserId = userId, ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId, LastContributionDate = DateTime.Now },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);

            return change.ValueChangeCount;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);

            if (SpeciesRepository.IsConcurrencyViolation(exception))
            {
                // The message is the procedure's own, for example "The profile version section has been edited by another user".
                throw new ConcurrencyException(exception.Message, exception);
            }

            logger.StoredProcedureFailed(exception, storedProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSectionContribution>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileSectionStoredProcedures.GetProfileVersionSectionContributions,
                    new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // ProfileVersionId, UserId, ProfileSectionId, UserName, Organisation, FullName, LastContributionDate.
            var contributions = new List<ProfileSectionContribution>();
            while (await reader.ReadAsync(cancellationToken))
            {
                contributions.Add(new ProfileSectionContribution
                {
                    UserId = reader.GetGuid(1),
                    UserName = reader.ReadString(3),
                    OrganisationName = reader.ReadString(4),
                    FullName = reader.ReadString(5),
                    LastContributionDate = reader.GetDateTime(6)
                });
            }

            return contributions;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileVersionSectionContributions);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileReviewComment>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileSectionStoredProcedures.GetProfileSectionReviewComments,
                    new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            var comments = new List<ProfileReviewComment>();
            while (await reader.ReadAsync(cancellationToken))
            {
                comments.Add(new ProfileReviewComment
                {
                    Id = reader.GetGuid(0),
                    CommentDate = reader.GetDateTime(1),
                    CommentSubject = reader.ReadString(2),
                    CommentReference = reader.ReadString(3),
                    CommentText = reader.ReadString(4),
                    UserName = reader.ReadString(5),
                    UserFullName = reader.ReadString(6),
                    ParentId = reader.ReadGuid(9),
                    Level = reader.ReadInt32(10),
                    ChildCount = reader.ReadInt32(12),
                    LastUpdated = reader.ReadRowVersion(8)
                });
            }

            return comments;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileSectionReviewComments);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfileSectionRevisionInfo?> GetProfileVersionSectionRevisionInfoAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileSectionStoredProcedures.GetProfileVersionSection,
                    new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Result set 1: profiled species - not needed here.
            await reader.NextResultAsync(cancellationToken);

            return await ReadRevisionInfoAsync(reader, cancellationToken);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileVersionSection);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProfileReviewLookupItem>> GetReviewFrequenciesAsync(CancellationToken cancellationToken) =>
        ReadReviewLookupAsync(ProfileSectionStoredProcedures.GetSectionReviewFrequencies, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ProfileReviewLookupItem>> GetReviewStatusesAsync(CancellationToken cancellationToken) =>
        ReadReviewLookupAsync(ProfileSectionStoredProcedures.GetSectionReviewStatuses, cancellationToken);

    /// <inheritdoc />
    public async Task<int> UpdateProfileVersionSectionRevisionsAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionUpdate> updates,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var update in updates)
            {
                var parameters = new DynamicParameters();
                parameters.Add("@ProfileVersionId", profileVersionId, DbType.Guid);
                parameters.Add("@ProfileSectionId", update.ProfileSectionId, DbType.Guid);
                parameters.Add("@TechnicalReviewFrequency", update.VaReviewFrequencyId, DbType.Guid);
                parameters.Add("@PolicyReviewFrequency", update.PolicyReviewFrequencyId, DbType.Guid);
                parameters.Add("@AuthorReviewFrequency", update.AuthorReviewFrequencyId, DbType.Guid);
                parameters.Add("@NextTechnicalReview", update.NextVaReview, DbType.DateTime);
                parameters.Add("@NextPolicyReview", update.NextPolicyReview, DbType.DateTime);
                parameters.Add("@NextAuthorReview", update.NextAuthorReview, DbType.DateTime);
                parameters.Add("@LastUpdated", update.LastUpdated, DbType.Binary, size: RowVersionLength);
                parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);
                parameters.Add("@PolicyReviewStatusId", update.PolicyReviewStatusId, DbType.Guid);
                parameters.Add("@TechnicalReviewStatusId", update.VaReviewStatusId, DbType.Guid);
                parameters.Add("@AuthorReviewStatusId", update.AuthorReviewStatusId, DbType.Guid);
                parameters.Add("@PolicyReviewCompleted", update.PolicyReviewCompleted, DbType.DateTime);
                parameters.Add("@TechnicalReviewCompleted", update.VaReviewCompleted, DbType.DateTime);
                parameters.Add("@AuthorReviewCompleted", update.AuthorReviewCompleted, DbType.DateTime);

                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileSectionStoredProcedures.UpdateProfileVersionSection,
                    parameters,
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileSectionStoredProcedures.LogProfileVersionSectionContribution,
                    new { UserId = userId, ProfileVersionId = profileVersionId, update.ProfileSectionId, LastContributionDate = DateTime.Now },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);

            return updates.Count;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);

            if (SpeciesRepository.IsConcurrencyViolation(exception))
            {
                throw new ConcurrencyException(
                    $"The revision dates of profile version '{profileVersionId}' could not be saved: a section was edited by another user, or the version is not a current draft. Re-read the revision dates and try again.",
                    exception);
            }

            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.UpdateProfileVersionSection);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ReviewCommentUser?> GetReviewCommentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            // No stored procedure resolves a user by id, so this is a parameterised text query.
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    ProfileSectionStoredProcedures.GetReviewCommentUser,
                    new { Id = userId },
                    commandType: CommandType.Text,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ReviewCommentUser { UserName = reader.ReadString(0), FullName = reader.ReadString(1) };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, "User lookup");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> InsertReviewCommentAsync(NewProfileReviewComment comment, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", comment.Id, DbType.Guid);
            parameters.Add("@ProfileVersionId", comment.ProfileVersionId, DbType.Guid);
            parameters.Add("@ProfileSectionId", comment.ProfileSectionId, DbType.Guid);
            parameters.Add("@CommentDate", comment.CommentDate, DbType.DateTime);
            parameters.Add("@Subject", comment.Subject, DbType.String);
            parameters.Add("@CommentReference", comment.CommentReference, DbType.String);
            parameters.Add("@Comment", comment.CommentText, DbType.String);
            parameters.Add("@UserName", comment.UserName, DbType.String);
            parameters.Add("@ParentId", comment.ParentId, DbType.Guid);
            parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.InsertReviewComment,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.InsertReviewComment);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> UpdateReviewCommentAsync(
        Guid id,
        string subject,
        string commentReference,
        string commentText,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", id, DbType.Guid);
            parameters.Add("@Subject", subject, DbType.String);
            parameters.Add("@CommentReference", commentReference, DbType.String);
            parameters.Add("@Comment", commentText, DbType.String);
            parameters.Add("@LastUpdated", lastUpdated, DbType.Binary, size: RowVersionLength);
            parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.UpdateReviewComment,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            return parameters.Get<byte[]?>("@NewLastUpdated") ?? [];
        }
        catch (DbException exception)
        {
            if (SpeciesRepository.IsConcurrencyViolation(exception))
            {
                throw new ConcurrencyException(
                    $"The review comment '{id}' could not be saved: it was edited by another user. Re-read the comment and try again.",
                    exception);
            }

            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.UpdateReviewComment);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteReviewCommentAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.DeleteReviewComment,
                new { Id = id },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.DeleteReviewComment);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAllReviewCommentsAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.DeleteReviewCommentsByProfileSection,
                new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.DeleteReviewCommentsByProfileSection);
            throw;
        }
    }

    private static async Task WriteSectionRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionId,
        Guid profileSectionId,
        ProfileSectionChange change,
        CancellationToken cancellationToken)
    {
        // The procedure overwrites every revision column, so the current values are read inside the transaction and
        // written straight back unless the change blanks them.
        var current = await ReadSectionRevisionAsync(connection, transaction, profileVersionId, profileSectionId, cancellationToken);
        var kept = change.ClearRevision ? null : current;
        var expected = change.ExpectedLastUpdated is { Length: > 0 } token ? token : current?.LastUpdated ?? new byte[RowVersionLength];

        var parameters = new DynamicParameters();
        parameters.Add("@ProfileVersionId", profileVersionId, DbType.Guid);
        parameters.Add("@ProfileSectionId", profileSectionId, DbType.Guid);
        parameters.Add("@TechnicalReviewFrequency", kept?.VaReviewFrequencyId, DbType.Guid);
        parameters.Add("@PolicyReviewFrequency", kept?.PolicyReviewFrequencyId, DbType.Guid);
        parameters.Add("@AuthorReviewFrequency", kept?.AuthorReviewFrequencyId, DbType.Guid);
        parameters.Add("@NextTechnicalReview", kept?.NextVaReview, DbType.DateTime);
        parameters.Add("@NextPolicyReview", kept?.NextPolicyReview, DbType.DateTime);
        parameters.Add("@NextAuthorReview", kept?.NextAuthorReview, DbType.DateTime);
        parameters.Add("@LastUpdated", expected, DbType.Binary, size: RowVersionLength);
        parameters.Add("@NewLastUpdated", null, DbType.Binary, ParameterDirection.Output, RowVersionLength);
        parameters.Add("@PolicyReviewStatusId", kept?.PolicyReviewStatusId, DbType.Guid);
        parameters.Add("@TechnicalReviewStatusId", kept?.VaReviewStatusId, DbType.Guid);
        parameters.Add("@AuthorReviewStatusId", kept?.AuthorReviewStatusId, DbType.Guid);

        // The section read does not return the completed dates, so, as in the legacy page, they are written blank.
        parameters.Add("@PolicyReviewCompleted", null, DbType.DateTime);
        parameters.Add("@TechnicalReviewCompleted", null, DbType.DateTime);
        parameters.Add("@AuthorReviewCompleted", null, DbType.DateTime);

        await connection.ExecuteAsync(new CommandDefinition(
            ProfileSectionStoredProcedures.UpdateProfileVersionSection,
            parameters,
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    private static async Task<ProfileSectionRevisionInfo?> ReadSectionRevisionAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                ProfileSectionStoredProcedures.GetProfileVersionSection,
                new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken),
            CommandBehavior.Default);

        // Result set 1 is the profiled species; result set 2 is the section's revision data.
        await reader.NextResultAsync(cancellationToken);

        return await ReadRevisionInfoAsync(reader, cancellationToken);
    }

    /// <summary>Reads the section's revision data from result set 2 of <c>spgProfileVersionSection</c>: ProfileSectionId,
    /// TechnicalReviewFrequency, PolicyReviewFrequency, AuthorReviewFrequency (all Guid FKs to luSectionReviewFrequency),
    /// NextTechnicalReview, NextPolicyReview, NextAuthorReview, LastUpdated, ParentId, PolicyReviewStatusId,
    /// TechnicalReviewStatusId, AuthorReviewStatusId (FKs to luReviewStatus). The database's "Technical" review is the
    /// UI's "VA" review.</summary>
    private static async Task<ProfileSectionRevisionInfo?> ReadRevisionInfoAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProfileSectionRevisionInfo
        {
            ProfileSectionId = reader.GetGuid(0),
            VaReviewFrequencyId = reader.ReadNullableGuid(1),
            PolicyReviewFrequencyId = reader.ReadNullableGuid(2),
            AuthorReviewFrequencyId = reader.ReadNullableGuid(3),
            NextVaReview = reader.ReadNullableDateTime(4),
            NextPolicyReview = reader.ReadNullableDateTime(5),
            NextAuthorReview = reader.ReadNullableDateTime(6),
            LastUpdated = reader.ReadRowVersion(7),
            PolicyReviewStatusId = reader.ReadNullableGuid(9),
            VaReviewStatusId = reader.ReadNullableGuid(10),
            AuthorReviewStatusId = reader.ReadNullableGuid(11)
        };
    }

    private static Task<int> ClearValueAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionId,
        ProfileFieldClearInstruction instruction,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            instruction.IsMultiValueList
                ? ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue
                : ProfileSectionStoredProcedures.ClearProfileVersionFieldValue,
            new
            {
                ProfileVersionId = profileVersionId,
                instruction.ProfileFieldId,
                instruction.SpeciesId,
                instruction.ProfileVersionQuestionRowId
            },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

    private static async Task SaveValueAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid profileVersionId,
        ProfileFieldSaveInstruction instruction,
        CancellationToken cancellationToken)
    {
        if (instruction.IsMultiValueList)
        {
            // A "MultiValueList" field is stored as one row per selected value, so the
            // existing rows are replaced wholesale rather than upserted individually.
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue,
                new { ProfileVersionId = profileVersionId, instruction.ProfileFieldId, SpeciesId = (Guid?)null, ProfileVersionQuestionRowId = (Guid?)null },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            foreach (var listValue in instruction.MultiListValues)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    ProfileSectionStoredProcedures.InsertProfileVersionFieldMultiValue,
                    new { ProfileVersionId = profileVersionId, instruction.ProfileFieldId, SpeciesId = (Guid?)null, ProfileVersionQuestionRowId = (Guid?)null, ListValue = listValue },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            return;
        }

        // NULL-safe upsert/delete: inserts a new row, updates the existing row, or
        // deletes it when every value parameter is left NULL.
        await connection.ExecuteAsync(new CommandDefinition(
            ProfileSectionStoredProcedures.UpsertProfileVersionFieldValue,
            new
            {
                ProfileVersionId = profileVersionId,
                instruction.ProfileFieldId,
                SpeciesId = (Guid?)null,
                ProfileVersionQuestionRowId = (Guid?)null,
                instruction.BooleanValue,
                instruction.ListValue,
                instruction.DecimalValue,
                instruction.DateValue,
                instruction.TextValue
            },
            transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<ProfileReviewLookupItem>> ReadReviewLookupAsync(
        string storedProcedure,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(
                new CommandDefinition(
                    storedProcedure,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken),
                CommandBehavior.Default);

            // Id, display text, sequence number.
            var items = new List<ProfileReviewLookupItem>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new ProfileReviewLookupItem { Id = reader.GetGuid(0), Name = reader.ReadString(1) });
            }

            return items;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, storedProcedure);
            throw;
        }
    }

    private static ProfileQuestionnaireMetadata BuildMetadata(
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

        return new ProfileQuestionnaireMetadata
        {
            Sections = [.. sections.Select(section => new ProfileSectionMetadata
            {
                Id = section.Id,
                Name = section.Name,
                ShortName = section.ShortName,
                SectionNumber = section.SectionNumber,
                Questions = questionsBySection.TryGetValue(section.Id, out var sectionQuestions)
                    ? [.. sectionQuestions.Select(question => new ProfileQuestionMetadata
                    {
                        Id = question.Id,
                        SectionId = question.SectionId,
                        ShortName = question.ShortName,
                        QuestionNumber = question.QuestionNumber,
                        IsPerSpecies = question.IsPerSpecies,
                        IsRepeating = question.IsRepeating,
                        Fields = fieldsByQuestion.TryGetValue(question.Id, out var questionFields)
                            ? [.. questionFields.Select(field => new ProfileFieldMetadata
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
                                IsReadOnlyCurrentSituation = field.IsReadOnlyCurrentSituation,
                                IsReadOnlyScenario = field.IsReadOnlyScenario
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
                $"{nameof(ProfileSectionRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    private sealed record SectionMetadataRow(Guid Id, string Name, string ShortName, int SectionNumber);

    private sealed record QuestionMetadataRow(
        Guid SectionId,
        Guid Id,
        string ShortName,
        int QuestionNumber,
        bool IsPerSpecies,
        bool IsRepeating);

    private sealed record FieldMetadataRow(
        Guid QuestionId,
        Guid Id,
        string ShortName,
        int FieldNumber,
        Guid DataFieldTypeId,
        string DataTypeName,
        bool IsMandatory,
        Guid ReferenceTableId,
        bool ReferenceTableIsMaintainable,
        string Name,
        bool IsReadOnlyCurrentSituation,
        bool IsReadOnlyScenario);
}
