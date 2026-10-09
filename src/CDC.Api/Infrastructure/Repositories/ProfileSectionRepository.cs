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
                    r.ReadString(6),
                    r.ReadBoolean(7),
                    r.ReadGuid(8),
                    r.ReadBoolean(9),
                    r.ReadString(11)),
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
