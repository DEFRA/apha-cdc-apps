using System.Data;
using System.Data.Common;
using System.Globalization;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;
using Dapper;

namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Dapper implementation of <see cref="IProfileReportContentRepository"/>. The stored procedures
/// return several result sets with duplicate or unnamed columns, so they are read positionally.
/// </summary>
/// <param name="connectionFactory">Opens connections to the Surveillance Profiles database.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileReportContentRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileReportContentRepository> logger)
    : IProfileReportContentRepository
{
    private const int QuestionIdOrdinal = 1;
    private const int FieldGroupIdOrdinal = 17;

    /// <inheritdoc />
    public async Task<ReportQuestionnaire> GetQuestionnaireAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                ProfileReportStoredProcedures.GetQuestionnaireMetadata,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var sections = await ReadSectionsAsync(reader, cancellationToken);
            var questions = await reader.NextResultAsync(cancellationToken) ? await ReadQuestionsAsync(reader, cancellationToken) : [];
            var fieldsByQuestion = await reader.NextResultAsync(cancellationToken)
                ? await ReadFieldsAsync(reader, QuestionIdOrdinal, cancellationToken)
                : [];
            var childrenByGroup = await reader.NextResultAsync(cancellationToken)
                ? await ReadFieldsAsync(reader, FieldGroupIdOrdinal, cancellationToken)
                : [];

            return Build(sections, questions, fieldsByQuestion, childrenByGroup);
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.GetQuestionnaireMetadata);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ReportSectionAnswers> GetSectionAnswersAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                ProfileReportStoredProcedures.GetProfileVersionSection,
                new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var species = await ReadSpeciesAsync(reader, cancellationToken);

            // Result set 2 holds revision dates and a timestamp, which a report does not print.
            await reader.NextResultAsync(cancellationToken);

            var names = await reader.NextResultAsync(cancellationToken) ? await ReadQuestionNamesAsync(reader, cancellationToken) : [];
            var rows = await reader.NextResultAsync(cancellationToken) ? await ReadRowsAsync(reader, cancellationToken) : [];
            var values = await reader.NextResultAsync(cancellationToken) ? await ReadValuesAsync(reader, cancellationToken) : [];

            return new ReportSectionAnswers { ProfiledSpecies = species, QuestionNames = names, Rows = rows, Values = values };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileReportStoredProcedures.GetProfileVersionSection);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportReferenceItem>> GetReferenceItemsAsync(Guid referenceTableId, bool isMaintainable, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var storedProcedure = isMaintainable
            ? ProfileReportStoredProcedures.GetReferenceValuesByTable
            : ProfileReportStoredProcedures.GetLookupValuesByTable;

        try
        {
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                storedProcedure,
                new { ReferenceTableId = referenceTableId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            // Maintainable: Id, LookupValue, EffectiveDateFrom, EffectiveDateTo, IsInUse, IsActive. Lookup: Id, LookupValue.
            var items = new List<ReportReferenceItem>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new ReportReferenceItem(reader.GetGuid(0), reader.ReadString(1), !isMaintainable || reader.ReadBoolean(5)));
            }

            return items;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, storedProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProfilePrioritisationScoresDto> GetPrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var storedProcedure = ProfileReportStoredProcedures.GetPrioritisationCategoryScores;

        try
        {
            var categoryScores = await ReadScoresAsync(connection, storedProcedure, profileVersionId, ReadCategoryScore, cancellationToken);

            storedProcedure = ProfileReportStoredProcedures.GetPrioritisationCriterionScores;
            var criterionScores = await ReadScoresAsync(connection, storedProcedure, profileVersionId, ReadCriterionScore, cancellationToken);

            storedProcedure = ProfileReportStoredProcedures.GetPrioritisedSpecies;
            var species = await ReadScoresAsync(connection, storedProcedure, profileVersionId, ReadPrioritisedSpecies, cancellationToken);

            return new ProfilePrioritisationScoresDto { CategoryScores = categoryScores, CriterionScores = criterionScores, Species = species };
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, storedProcedure);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileQuestionGuidanceDto>> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        try
        {
            var guidance = new List<ProfileQuestionGuidanceDto>();

            foreach (var questionId in questionIds)
            {
                await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                    ProfileQuestionStoredProcedures.GetProfileQuestion,
                    new { Id = questionId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

                // Id, Name, ShortName, QuestionNumber, UserGuidance, LastUpdated, NonTechnicalName.
                if (await reader.ReadAsync(cancellationToken))
                {
                    guidance.Add(new ProfileQuestionGuidanceDto { QuestionId = questionId, UserGuidance = reader.ReadString(4) });
                }
            }

            return guidance;
        }
        catch (DbException exception)
        {
            logger.StoredProcedureFailed(exception, ProfileQuestionStoredProcedures.GetProfileQuestion);
            throw;
        }
    }

    private static async Task<IReadOnlyList<T>> ReadScoresAsync<T>(
        DbConnection connection,
        string storedProcedure,
        Guid profileVersionId,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
            storedProcedure,
            new { ProfileVersionId = profileVersionId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    // CategoryId, CategoryName, SpeciesId, SpeciesName, DataIncomplete, Score, Rank, IsProfiledSpecies.
    private static ProfileCategoryScoreDto ReadCategoryScore(DbDataReader reader) => new()
    {
        CategoryId = reader.GetGuid(0),
        CategoryName = reader.ReadString(1),
        SpeciesId = reader.GetGuid(2),
        SpeciesName = reader.ReadString(3),
        DataIncomplete = reader.ReadBoolean(4),
        Score = reader.ReadInt32(5),
        Rank = reader.ReadInt32(6),
        IsProfiledSpecies = reader.ReadBoolean(7)
    };

    // CategoryId, CategoryName, CriterionId, CriterionCode, CriterionName, SpeciesId, SpeciesName, DataIncomplete,
    // IrrelevantCategory, CriterionValue, Score, CategoryDisplayName.
    private static ProfileCriterionScoreDto ReadCriterionScore(DbDataReader reader) => new()
    {
        CategoryId = reader.GetGuid(0),
        CategoryName = reader.ReadString(1),
        CriterionId = reader.GetGuid(2),
        CriterionCode = reader.ReadString(3),
        CriterionName = reader.ReadString(4),
        SpeciesId = reader.GetGuid(5),
        SpeciesName = reader.ReadString(6),
        DataIncomplete = reader.ReadBoolean(7),
        IrrelevantCategory = reader.ReadBoolean(8),
        CriterionValue = reader.ReadString(9),
        Score = reader.ReadInt32(10),
        CategoryDisplayName = reader.ReadString(11)
    };

    // SpeciesId, Name.
    private static ProfilePrioritisedSpeciesDto ReadPrioritisedSpecies(DbDataReader reader) => new()
    {
        SpeciesId = reader.GetGuid(0),
        Name = reader.ReadString(1)
    };

    // Result set 1: Id, Name, ShortName, SectionNumber, Note.
    private static async Task<List<SectionRow>> ReadSectionsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var sections = new List<SectionRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            sections.Add(new SectionRow(reader.GetGuid(0), reader.ReadString(1), reader.ReadString(2), reader.ReadInt32(3), reader.ReadString(4)));
        }

        return sections;
    }

    // Result set 2: SectionId, Id, ShortName, QuestionNumber, IsPerSpecies, IsRepeating, IrrelevanceFieldId, IrrelevanceValueId, IrrelevanceFlag.
    private static async Task<List<QuestionRow>> ReadQuestionsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var questions = new List<QuestionRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            questions.Add(new QuestionRow(
                reader.GetGuid(0),
                new ReportQuestion
                {
                    Id = reader.GetGuid(1),
                    QuestionNumber = reader.ReadInt32(3),
                    IsPerSpecies = reader.ReadBoolean(4),
                    IsRepeating = reader.ReadBoolean(5),
                    IrrelevanceFieldId = reader.ReadGuid(6),
                    IrrelevanceValueId = reader.ReadGuid(7),
                    IrrelevanceFlag = reader.ReadBoolean(8)
                }));
        }

        return questions;
    }

    // Result sets 3 and 4: SectionId, QuestionId, Id, ShortName, FieldNumber, DataFieldTypeId, DataTypeName, IsMandatory,
    // ReferenceTableId, IsMaintainable, AffectsRelevancy, Name, ... (result set 4 adds ProfileFieldGroupId as column 17).
    private static async Task<Dictionary<Guid, List<ReportField>>> ReadFieldsAsync(DbDataReader reader, int keyOrdinal, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<Guid, List<ReportField>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.ReadGuid(keyOrdinal);

            if (!fields.TryGetValue(key, out var list))
            {
                list = [];
                fields[key] = list;
            }

            list.Add(new ReportField
            {
                Id = reader.GetGuid(2),
                FieldNumber = reader.ReadInt32(4),
                DataTypeName = reader.ReadString(6),
                ReferenceTableId = reader.ReadGuid(8),
                ReferenceTableIsMaintainable = reader.ReadBoolean(9),
                Name = reader.ReadString(11)
            });
        }

        return fields;
    }

    // Result set 1: Id, Name, IsActive.
    private static async Task<List<ReportSpecies>> ReadSpeciesAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var species = new List<ReportSpecies>();

        while (await reader.ReadAsync(cancellationToken))
        {
            species.Add(new ReportSpecies(reader.GetGuid(0), reader.ReadString(1)));
        }

        return species;
    }

    // Result set 3: Id, Name, NonTechnicalName.
    private static async Task<List<ProfileQuestionName>> ReadQuestionNamesAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var names = new List<ProfileQuestionName>();

        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(new ProfileQuestionName { Id = reader.GetGuid(0), Name = reader.ReadString(1), NonTechnicalName = reader.ReadString(2) });
        }

        return names;
    }

    // Result set 4: Id, ProfileQuestionId, SequenceNumber.
    private static async Task<List<ReportQuestionRow>> ReadRowsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var rows = new List<ReportQuestionRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ReportQuestionRow(reader.GetGuid(0), reader.ReadGuid(1), Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    // Result set 5: Id, SpeciesId, RowId, BooleanValue, ListValue, DecimalValue, DateValue, TextValue, ProfileFieldId,
    // ProfileFieldGroupId, QuestionNumber, QuestionId, FieldNumber.
    private static async Task<List<ReportFieldValue>> ReadValuesAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var values = new List<ReportFieldValue>();

        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(new ReportFieldValue
            {
                SpeciesId = reader.ReadNullableGuid(1),
                RowId = reader.ReadNullableGuid(2),
                BooleanValue = reader.ReadNullableBoolean(3),
                ListValue = reader.ReadNullableGuid(4),
                DecimalValue = reader.ReadNullableDecimal(5),
                DateValue = reader.ReadNullableDateTime(6),
                TextValue = reader.ReadNullableString(7),
                FieldId = reader.GetGuid(8),
                FieldGroupId = reader.ReadGuid(9),
                QuestionId = reader.GetGuid(11)
            });
        }

        return values;
    }

    private static ReportQuestionnaire Build(
        List<SectionRow> sections,
        List<QuestionRow> questions,
        Dictionary<Guid, List<ReportField>> fieldsByQuestion,
        Dictionary<Guid, List<ReportField>> childrenByGroup)
    {
        ReportField WithChildren(ReportField field) =>
            childrenByGroup.TryGetValue(field.Id, out var children)
                ? field with { Children = [.. children.OrderBy(child => child.FieldNumber)] }
                : field;

        ReportQuestion WithFields(ReportQuestion question) =>
            question with
            {
                Fields = fieldsByQuestion.TryGetValue(question.Id, out var fields)
                    ? [.. fields.OrderBy(field => field.FieldNumber).Select(WithChildren)]
                    : []
            };

        return new ReportQuestionnaire
        {
            Sections =
            [
                .. sections.OrderBy(section => section.SectionNumber).Select(section => new ReportSection
                {
                    Id = section.Id,
                    SectionNumber = section.SectionNumber,
                    Name = section.Name,
                    ShortName = section.ShortName,
                    Note = section.Note,
                    Questions =
                    [
                        .. questions
                            .Where(question => question.SectionId == section.Id)
                            .Select(question => WithFields(question.Question))
                            .OrderBy(question => question.QuestionNumber)
                    ]
                })
            ]
        };
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is not DbConnection dbConnection)
        {
            connection.Dispose();
            throw new InvalidOperationException(
                $"{nameof(ProfileReportContentRepository)} requires a {nameof(DbConnection)} so that database calls can be awaited.");
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

    private sealed record SectionRow(Guid Id, string Name, string ShortName, int SectionNumber, string Note);

    private sealed record QuestionRow(Guid SectionId, ReportQuestion Question);
}
