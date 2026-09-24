using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species.Commands;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.Species;

public class SpeciesRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<SpeciesRepository>> logger = new();

    public SpeciesRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private SpeciesRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetAllSpeciesAsync_ExecutesStoredProcedureAndMapsRows()
    {
        connection.Script(SpeciesStoredProcedures.GetAllSpecies, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "ParentId", "Name", "IsActive", "IsInUse"],
                    [
                        [SpeciesTestData.SpeciesId, null, "Cattle", true, true],
                        [SpeciesTestData.SectionId, SpeciesTestData.SpeciesId, "Dairy cattle", false, true]
                    ])
            ]
        });

        var species = await CreateRepository().GetAllSpeciesAsync(CancellationToken.None);

        species.Should().HaveCount(2);
        species[0].Description.Should().Be("Cattle");
        // The procedure returns NULL for a root species; the legacy reader mapped that to Guid.Empty.
        species[0].ParentId.Should().Be(Guid.Empty);
        species[1].ParentId.Should().Be(SpeciesTestData.SpeciesId);
        species[1].IsActive.Should().BeFalse();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(SpeciesStoredProcedures.GetAllSpecies);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetAllSelectedSpeciesAsync_PassesDiseaseNameParameter()
    {
        connection.Script(SpeciesStoredProcedures.GetAllSelectedSpecies, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "ParentId", "Name", "IsActive", "IsInUse", "DiseaseName", "Disease1", "Disease2", "Disease3", "Disease4", "Disease5", "FilterNumber"],
                    [[SpeciesTestData.SpeciesId, Guid.Empty, "Cattle", true, true, "Bovine tuberculosis", 0, 1, 2, 1, "Other", 7L]])
            ]
        });

        var species = await CreateRepository().GetAllSelectedSpeciesAsync("Bovine tuberculosis", CancellationToken.None);

        species.Should().ContainSingle();
        species[0].DiseaseName.Should().Be("Bovine tuberculosis");
        species[0].FilterNumber.Should().Be(7);

        connection.Executed.Should().ContainSingle()
            .Which.Parameters.Should().ContainKey("DiseaseName")
            .WhoseValue.Should().Be("Bovine tuberculosis");
    }

    [Fact]
    public async Task GetSpeciesMetadataAsync_AssemblesSectionsQuestionsAndFields()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesMetadata, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "Name", "ShortName", "SectionNumber"],
                    [[SpeciesTestData.SectionId, "Epidemiology", "Epi", 1]]),
                new FakeResultSet(
                    ["SpeciesSectionId", "Id", "Name", "QuestionNumber", "ShortName"],
                    [[SpeciesTestData.SectionId, SpeciesTestData.QuestionId, "Is it endemic?", 1, "Endemic"]]),
                // Note the duplicate "Id" column: the question id then the field id. Dapper cannot
                // map this by name, which is why the repository reads it positionally.
                new FakeResultSet(
                    ["SpeciesSectionId", "Id", "Id", "Name", "ShortName", "FieldNumber", "DataFieldTypeId", "DataTypeName", "IsMandatory", "ReferenceTableId", "ReferenceTableIsMaintainable"],
                    [
                        [
                            SpeciesTestData.SectionId,
                            SpeciesTestData.QuestionId,
                            SpeciesTestData.FieldId,
                            "Endemic",
                            "End",
                            1,
                            Guid.Empty,
                            "Boolean",
                            true,
                            null,
                            false
                        ]
                    ])
            ]
        });

        var metadata = await CreateRepository().GetSpeciesMetadataAsync(CancellationToken.None);

        var section = metadata.Sections.Should().ContainSingle().Subject;
        section.Name.Should().Be("Epidemiology");

        var question = section.Questions.Should().ContainSingle().Subject;
        question.Id.Should().Be(SpeciesTestData.QuestionId);

        var field = question.Fields.Should().ContainSingle().Subject;
        field.Id.Should().Be(SpeciesTestData.FieldId);
        field.QuestionId.Should().Be(SpeciesTestData.QuestionId);
        field.DataTypeName.Should().Be("Boolean");
        field.ReferenceTableId.Should().Be(Guid.Empty);
        // The column is absent in older databases, so it falls back to zero.
        field.EditorFieldType.Should().Be(0);
    }

    [Fact]
    public async Task GetSpeciesAnswerDataAsync_ReturnsNull_WhenSpeciesDoesNotExist()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesAnswerData, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("LastUpdated", "Name", "SpeciesId")]
        });

        var answerData = await CreateRepository().GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        answerData.Should().BeNull();
    }

    [Fact]
    public async Task GetSpeciesAnswerDataAsync_MapsRowVersionSectionsAndValues()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesAnswerData, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["LastUpdated", "Name", "SpeciesId"],
                    [[SpeciesTestData.RowVersion, "Cattle", SpeciesTestData.SpeciesId]]),
                new FakeResultSet(["Id"], [[SpeciesTestData.SectionId]]),
                new FakeResultSet(
                    ["SpeciesSectionId", "Id", "BooleanValue", "ListValue", "TextValue", "Id", "FieldNumber"],
                    [
                        [SpeciesTestData.SectionId, SpeciesTestData.FieldId, true, null, null, SpeciesTestData.QuestionId, 1],
                        [SpeciesTestData.SectionId, SpeciesTestData.ListValueId, null, SpeciesTestData.ListValueId, null, SpeciesTestData.QuestionId, 2],
                        // A value for an unknown section is skipped rather than failing the request.
                        [Guid.NewGuid(), Guid.NewGuid(), null, null, "orphan", SpeciesTestData.QuestionId, 3]
                    ])
            ]
        });

        var answerData = await CreateRepository().GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        answerData.Should().NotBeNull();
        answerData!.SpeciesName.Should().Be("Cattle");
        answerData.LastUpdated.Should().Equal(SpeciesTestData.RowVersion);

        var section = answerData.Sections.Should().ContainSingle().Subject;
        section.FieldValues.Should().HaveCount(2);
        section.FieldValues[0].BooleanValue.Should().BeTrue();
        section.FieldValues[0].ListValue.Should().BeNull();
        section.FieldValues[1].ListValue.Should().Be(SpeciesTestData.ListValueId);
        section.FieldValues[1].FieldNumber.Should().Be(2);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataAsync_RunsEveryProcedureInOneTransactionAndCommits()
    {
        ScriptSuccessfulRowVersionUpdate();

        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.Boolean, BooleanValue = true },
            new SpeciesFieldValueChange
            {
                FieldId = SpeciesTestData.ListValueId,
                Kind = SpeciesFieldValueKind.MultiValue,
                MultiValues = [SpeciesTestData.ListValueId, SpeciesTestData.QuestionId]
            });

        var rowVersion = await CreateRepository().UpdateSpeciesAnswerDataAsync(command, CancellationToken.None);

        rowVersion.Should().Equal(SpeciesTestData.NewRowVersion);

        connection.Executed.Select(executed => executed.CommandText).Should().Equal(
            SpeciesStoredProcedures.UpdateSpeciesAnswerData,
            SpeciesStoredProcedures.UpdateSpeciesFieldValue,
            SpeciesStoredProcedures.DeleteSpeciesFieldMultiValue,
            SpeciesStoredProcedures.InsertSpeciesFieldMultiValue,
            SpeciesStoredProcedures.InsertSpeciesFieldMultiValue,
            SpeciesStoredProcedures.CalculatePrioritisationScore);

        connection.Executed.Should().OnlyContain(executed => executed.HadTransaction);

        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.Committed.Should().BeTrue();
        transaction.RolledBack.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataAsync_SendsOnlyTheParameterMatchingTheChangeKind()
    {
        ScriptSuccessfulRowVersionUpdate();

        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.Text, TextValue = "Endemic in GB" });

        await CreateRepository().UpdateSpeciesAnswerDataAsync(command, CancellationToken.None);

        var fieldUpdate = connection.Executed
            .Single(executed => executed.CommandText == SpeciesStoredProcedures.UpdateSpeciesFieldValue);

        // Dapper strips the "@" prefix when it builds the command parameters.
        fieldUpdate.Parameters.Should().ContainKey("TextValue").WhoseValue.Should().Be("Endemic in GB");
        fieldUpdate.Parameters.Should().NotContainKey("BooleanValue");
        fieldUpdate.Parameters.Should().NotContainKey("ListValue");
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataAsync_ClearsAnswer_WhenChangeKindIsNone()
    {
        ScriptSuccessfulRowVersionUpdate();

        var command = SpeciesTestData.UpdateCommand(
            new SpeciesFieldValueChange { FieldId = SpeciesTestData.FieldId, Kind = SpeciesFieldValueKind.None });

        await CreateRepository().UpdateSpeciesAnswerDataAsync(command, CancellationToken.None);

        var fieldUpdate = connection.Executed
            .Single(executed => executed.CommandText == SpeciesStoredProcedures.UpdateSpeciesFieldValue);

        // No value parameters: the stored procedure defaults them to NULL and deletes the row.
        fieldUpdate.Parameters.Keys.Should().BeEquivalentTo("SpeciesId", "SpeciesFieldId");
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataAsync_RollsBackAndThrowsConcurrency_WhenRowVersionIsStale()
    {
        connection.Script(SpeciesStoredProcedures.UpdateSpeciesAnswerData, new FakeCommandScript
        {
            Throws = new FakeDbException("The species has been edited by another user")
        });

        var act = async () => await CreateRepository()
            .UpdateSpeciesAnswerDataAsync(SpeciesTestData.UpdateCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();

        var transaction = connection.Transactions.Should().ContainSingle().Subject;
        transaction.RolledBack.Should().BeTrue();
        transaction.Committed.Should().BeFalse();

        VerifyErrorLogged();
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataAsync_RollsBackAndRethrows_WhenAnotherDatabaseErrorOccurs()
    {
        ScriptSuccessfulRowVersionUpdate();
        connection.Script(SpeciesStoredProcedures.CalculatePrioritisationScore, new FakeCommandScript
        {
            Throws = new FakeDbException("Deadlock victim")
        });

        var act = async () => await CreateRepository()
            .UpdateSpeciesAnswerDataAsync(SpeciesTestData.UpdateCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<FakeDbException>()).WithMessage("Deadlock victim");

        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataAsync_RejectsNullCommand()
    {
        var act = async () => await CreateRepository().UpdateSpeciesAnswerDataAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAllSpeciesAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetAllSpecies, new FakeCommandScript
        {
            Throws = new FakeDbException("Invalid object name 'Species'")
        });

        var act = async () => await CreateRepository().GetAllSpeciesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task GetSpeciesMetadataAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesMetadata, new FakeCommandScript
        {
            Throws = new FakeDbException("Timeout expired")
        });

        var act = async () => await CreateRepository().GetSpeciesMetadataAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task GetSpeciesAnswerDataAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesAnswerData, new FakeCommandScript
        {
            Throws = new FakeDbException("Timeout expired")
        });

        var act = async () => await CreateRepository()
            .GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task GetAllSelectedSpeciesAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetAllSelectedSpecies, new FakeCommandScript
        {
            Throws = new FakeDbException("Timeout expired")
        });

        var act = async () => await CreateRepository().GetAllSelectedSpeciesAsync("Anthrax", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task GetAllSpeciesAsync_Throws_WhenTheFactoryDoesNotProvideADbConnection()
    {
        var repository = new SpeciesRepository(new NonDbConnectionFactory(), logger.Object);

        var act = async () => await repository.GetAllSpeciesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DbConnection*");
    }

    [Fact]
    public void IsConcurrencyViolation_RecognisesTheRaiserrorMessage()
    {
        SpeciesRepository.IsConcurrencyViolation(
            new FakeDbException("The species has been edited by another user")).Should().BeTrue();

        SpeciesRepository.IsConcurrencyViolation(new FakeDbException("Deadlock victim")).Should().BeFalse();
    }

    private void ScriptSuccessfulRowVersionUpdate() =>
        connection.Script(SpeciesStoredProcedures.UpdateSpeciesAnswerData, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["@NewLastUpdated"] = SpeciesTestData.NewRowVersion }
        });

    private void VerifyErrorLogged() =>
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

    [Fact]
    public async Task GetSpeciesByIdAsync_MapsRow()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Name", "ParentId", "IsActive", "IsInUse", "ChildCount", "ActiveChildCount", "ParentName", "LastUpdated"],
                    [["Dairy cattle", SpeciesTestData.SectionId, true, true, 0, 0, "Cattle", SpeciesTestData.RowVersion]])
            ]
        });

        var detail = await CreateRepository().GetSpeciesByIdAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Dairy cattle");
        detail.ParentName.Should().Be("Cattle");
        detail.LastUpdated.Should().Equal(SpeciesTestData.RowVersion);
    }

    [Fact]
    public async Task GetSpeciesByIdAsync_ReturnsNull_WhenSpeciesDoesNotExist()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesById, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Name", "ParentId", "IsActive", "IsInUse", "ChildCount", "ActiveChildCount", "ParentName", "LastUpdated")]
        });

        var detail = await CreateRepository().GetSpeciesByIdAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetSpeciesByIdAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesById, new FakeCommandScript
        {
            Throws = new FakeDbException("Timeout expired")
        });

        var act = async () => await CreateRepository().GetSpeciesByIdAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task GetSpeciesValidParentsAsync_MapsRows()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesValidParents, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "Name"], [[SpeciesTestData.SectionId, "Cattle"]])]
        });

        var validParents = await CreateRepository().GetSpeciesValidParentsAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        validParents.Should().ContainSingle();
        validParents[0].Name.Should().Be("Cattle");
    }

    [Fact]
    public async Task GetSpeciesValidParentsAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesValidParents, new FakeCommandScript
        {
            Throws = new FakeDbException("Timeout expired")
        });

        var act = async () => await CreateRepository().GetSpeciesValidParentsAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ExecutesProcedureThenRereadsTheNewRowVersion()
    {
        connection.Script(SpeciesStoredProcedures.UpdateSpecies, new FakeCommandScript());
        connection.Script(SpeciesStoredProcedures.GetSpeciesById, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Name", "ParentId", "IsActive", "IsInUse", "ChildCount", "ActiveChildCount", "ParentName", "LastUpdated"],
                    [["Dairy", SpeciesTestData.SectionId, true, true, 0, 0, "Cattle", SpeciesTestData.NewRowVersion]])
            ]
        });

        var command = SpeciesTestData.UpdateNameParentCommand();

        var rowVersion = await CreateRepository().UpdateSpeciesNameParentAsync(command, CancellationToken.None);

        rowVersion.Should().Equal(SpeciesTestData.NewRowVersion);

        connection.Executed.Select(executed => executed.CommandText).Should().Equal(
            SpeciesStoredProcedures.UpdateSpecies,
            SpeciesStoredProcedures.GetSpeciesById);

        var update = connection.Executed[0];
        update.Parameters.Should().ContainKey("UserId").WhoseValue.Should().Be(command.UserId);
        update.Parameters.Should().ContainKey("Reason").WhoseValue.Should().Be("Simplifying the name");
        update.Parameters.Should().NotContainKey("ChangedBy");
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_SendsNullParentId_ForARootSpecies()
    {
        connection.Script(SpeciesStoredProcedures.UpdateSpecies, new FakeCommandScript());

        var command = SpeciesTestData.UpdateNameParentCommand() with { ParentId = Guid.Empty };

        await CreateRepository().UpdateSpeciesNameParentAsync(command, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle(executed => executed.CommandText == SpeciesStoredProcedures.UpdateSpecies).Subject;
        executed.Parameters.Should().ContainKey("ParentId").WhoseValue.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_RejectsNullCommand()
    {
        var act = async () => await CreateRepository().UpdateSpeciesNameParentAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ThrowsConcurrency_WhenRowVersionIsStale()
    {
        connection.Script(SpeciesStoredProcedures.UpdateSpecies, new FakeCommandScript
        {
            Throws = new FakeDbException("The species has been edited by another user")
        });

        var act = async () => await CreateRepository()
            .UpdateSpeciesNameParentAsync(SpeciesTestData.UpdateNameParentCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
        VerifyErrorLogged();
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_Rethrows_WhenAnotherDatabaseErrorOccurs()
    {
        connection.Script(SpeciesStoredProcedures.UpdateSpecies, new FakeCommandScript
        {
            Throws = new FakeDbException("Deadlock victim")
        });

        var act = async () => await CreateRepository()
            .UpdateSpeciesNameParentAsync(SpeciesTestData.UpdateNameParentCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<FakeDbException>()).WithMessage("Deadlock victim");
        VerifyErrorLogged();
    }

    [Fact]
    public async Task GetSpeciesAuditTrailAsync_MapsRows_MostRecentFirst()
    {
        var earlier = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        connection.Script(SpeciesStoredProcedures.GetSpeciesAuditTrail, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "FullName", "LogDate", "Reason", "OldName", "NewName", "OldParent", "NewParent"],
                    [
                        [Guid.NewGuid(), "a.user", earlier, "First change", "Old", "Middle", "Cattle", "Cattle"],
                        [Guid.NewGuid(), "b.user", later, "Second change", "Middle", "New", "Cattle", "Cattle"]
                    ])
            ]
        });

        var entries = await CreateRepository().GetSpeciesAuditTrailAsync(CancellationToken.None);

        entries.Should().HaveCount(2);
        entries[0].ReasonForChange.Should().Be("Second change");
        entries[1].ReasonForChange.Should().Be("First change");
    }

    [Fact]
    public async Task GetSpeciesAuditTrailAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(SpeciesStoredProcedures.GetSpeciesAuditTrail, new FakeCommandScript
        {
            Throws = new FakeDbException("Timeout expired")
        });

        var act = async () => await CreateRepository().GetSpeciesAuditTrailAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
        VerifyErrorLogged();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class NonDbConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new Mock<IDbConnection>().Object;
    }
}
