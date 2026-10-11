using System.Data;
using System.Data.Common;
using CDC.Api.Domain.Entities;
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

            var sections = await ReadSectionMetadataAsync(reader, cancellationToken);
            var questions = await ReadQuestionMetadataAsync(reader, cancellationToken);
            var fields = await ReadFieldMetadataAsync(reader, cancellationToken);

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
            await reader.NextResultAsync(cancellationToken);

            // Result set 2: revision dates and timestamp - not needed for read-only browsing.
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

            // Result set 4: repeating question rows - not needed for read-only browsing.
            await reader.NextResultAsync(cancellationToken);

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
                FieldValues = fieldValues
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileVersionSection);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ClearProfileVersionSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldClearInstruction> instructions,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (instructions.Count == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var instruction in instructions)
            {
                var procedureName = instruction.IsMultiValueList
                    ? ProfileSectionStoredProcedures.ClearProfileVersionFieldMultiValue
                    : ProfileSectionStoredProcedures.ClearProfileVersionFieldValue;

                await connection.ExecuteAsync(new CommandDefinition(
                    procedureName,
                    new
                    {
                        ProfileVersionId = profileVersionId,
                        ProfileFieldId = instruction.ProfileFieldId,
                        instruction.SpeciesId,
                        instruction.ProfileVersionQuestionRowId
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            // Matches the legacy LogUserContribution call, which only fires when at least one
            // field value actually changed.
            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.LogProfileVersionSectionContribution,
                new { UserId = userId, ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId, LastContributionDate = DateTime.Now },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return instructions.Count;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.ClearProfileVersionFieldValue);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveProfileVersionSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldSaveInstruction> instructions,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (instructions.Count == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var instruction in instructions)
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
                }
                else
                {
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
            }

            await connection.ExecuteAsync(new CommandDefinition(
                ProfileSectionStoredProcedures.LogProfileVersionSectionContribution,
                new { UserId = userId, ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId, LastContributionDate = DateTime.Now },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return instructions.Count;
        }
        catch (DbException exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.UpsertProfileVersionFieldValue);
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
                    ChildCount = reader.ReadInt32(12)
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

            // Result set 2: ProfileSectionId, TechnicalReviewFrequency, PolicyReviewFrequency,
            // NextTechnicalReview, NextPolicyReview, LastUpdated, ParentId, PolicyReviewStatusId,
            // TechnicalReviewStatusId.
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ProfileSectionRevisionInfo
            {
                ProfileSectionId = reader.GetGuid(0),
                TechnicalReviewFrequencyMonths = reader.ReadNullableInt32(1),
                PolicyReviewFrequencyMonths = reader.ReadNullableInt32(2),
                NextTechnicalReview = reader.ReadNullableDateTime(3),
                NextPolicyReview = reader.ReadNullableDateTime(4)
            };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileSectionStoredProcedures.GetProfileVersionSection);
            throw;
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
                reader.ReadString(1),
                reader.ReadString(2),
                reader.ReadInt32(3)));
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
                reader.ReadString(2),
                reader.ReadInt32(3),
                reader.ReadBoolean(4),
                reader.ReadBoolean(5)));
        }

        return questions;
    }

    private static async Task<List<FieldMetadataRow>> ReadFieldMetadataAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
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
                reader.ReadString(3),
                reader.ReadInt32(4),
                reader.ReadGuid(5),
                reader.ReadString(6),
                reader.ReadBoolean(7),
                reader.ReadGuid(8),
                reader.ReadBoolean(9),
                reader.ReadString(11)));
        }

        return fields;
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
                                ReferenceTableIsMaintainable = field.ReferenceTableIsMaintainable
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
        string Name);
}
