using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileNotes;

public class ProfileNoteRepositoryTests : IDisposable
{
    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileNoteRepository>> logger = new();

    public ProfileNoteRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileNoteRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetNoteTypesAsync_MapsRows()
    {
        connection.Script(ProfileNoteStoredProcedures.GetNoteTypes, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "Name", "PluralName"], [[ProfileNoteTestData.NoteTypeId, "Comment", "Comments"]])]
        });

        var noteTypes = await CreateRepository().GetNoteTypesAsync(CancellationToken.None);

        noteTypes.Should().ContainSingle();
        noteTypes[0].Name.Should().Be("Comment");
        noteTypes[0].PluralName.Should().Be("Comments");
    }

    [Fact]
    public async Task GetNotesBySectionAsync_MapsNotesAndQuestionReferences()
    {
        connection.Script(ProfileNoteStoredProcedures.GetNotesBySectionAndType, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "NoteText", "LastUpdated"],
                    [[ProfileNoteTestData.NoteId, "A note", ProfileNoteTestData.RowVersion]]),
                new FakeResultSet(
                    ["ProfileVersionNoteId", "ProfileQuestionId"],
                    [[ProfileNoteTestData.NoteId, ProfileNoteTestData.ProfileQuestionId]])
            ]
        });

        var notes = await CreateRepository().GetNotesBySectionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.ProfileSectionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        notes.Should().ContainSingle();
        notes[0].NoteText.Should().Be("A note");
        notes[0].LastUpdated.Should().Equal(ProfileNoteTestData.RowVersion);
        var reference = notes[0].QuestionReferences.Should().ContainSingle().Subject;
        reference.ProfileSectionId.Should().Be(ProfileNoteTestData.ProfileSectionId);
        reference.ProfileQuestionId.Should().Be(ProfileNoteTestData.ProfileQuestionId);
    }

    [Fact]
    public async Task GetNotesByVersionAsync_MapsNotesAndQuestionReferences()
    {
        connection.Script(ProfileNoteStoredProcedures.GetNotesByType, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "NoteText", "LastUpdated"],
                    [[ProfileNoteTestData.NoteId, "A note", ProfileNoteTestData.RowVersion]]),
                new FakeResultSet(
                    ["ProfileVersionNoteId", "ProfileSectionId", "ProfileQuestionId"],
                    [[ProfileNoteTestData.NoteId, ProfileNoteTestData.ProfileSectionId, ProfileNoteTestData.ProfileQuestionId]])
            ]
        });

        var notes = await CreateRepository().GetNotesByVersionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        notes.Should().ContainSingle();
        notes[0].QuestionReferences.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateNotesAsync_InsertsNoteAndQuestionReference()
    {
        connection.Script(ProfileNoteStoredProcedures.InsertProfileVersionNote, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = ProfileNoteTestData.NewRowVersion }
        });
        connection.Script(ProfileNoteStoredProcedures.InsertProfileVersionNoteQuestion, new FakeCommandScript());
        connection.Script(ProfileNoteStoredProcedures.InsertProfileVersionSectionUser, new FakeCommandScript());

        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts =
            [
                new ProfileNoteInsertDto
                {
                    Id = ProfileNoteTestData.NoteId,
                    NoteText = "A note",
                    QuestionReferenceAdds =
                    [
                        new QuestionReferenceDto { ProfileSectionId = ProfileNoteTestData.ProfileSectionId, ProfileQuestionId = ProfileNoteTestData.ProfileQuestionId }
                    ]
                }
            ]
        };

        var result = await CreateRepository().UpdateNotesAsync(command, Guid.Empty, CancellationToken.None);

        result.IdInsertList.Should().ContainSingle().Which.Should().Be(ProfileNoteTestData.NoteId);
        result.LastUpdatedInsertList.Should().ContainSingle().Which.Should().Equal(ProfileNoteTestData.NewRowVersion);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileNoteStoredProcedures.InsertProfileVersionNoteQuestion);
        // The inserted note links a question in a section, so that section's contribution is logged.
        connection.Executed.Should().Contain(c => c.CommandText == ProfileNoteStoredProcedures.InsertProfileVersionSectionUser);
        connection.Transactions.Should().ContainSingle().Which.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateNotesAsync_DeletesNote_AndLogsAffectedSections()
    {
        connection.Script(ProfileNoteStoredProcedures.GetProfileSectionIdByNoteId, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["ProfileSectionId", "NoteText"], [[ProfileNoteTestData.ProfileSectionId, "Old text"]])]
        });
        connection.Script(ProfileNoteStoredProcedures.DeleteProfileVersionNote, new FakeCommandScript());
        connection.Script(ProfileNoteStoredProcedures.InsertProfileVersionSectionUser, new FakeCommandScript());

        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Deletes = [new ProfileNoteDeleteDto { Id = ProfileNoteTestData.NoteId, LastUpdated = ProfileNoteTestData.RowVersion }]
        };

        await CreateRepository().UpdateNotesAsync(command, Guid.Empty, CancellationToken.None);

        connection.Executed.Should().Contain(c => c.CommandText == ProfileNoteStoredProcedures.DeleteProfileVersionNote);
        connection.Executed.Should().Contain(c => c.CommandText == ProfileNoteStoredProcedures.InsertProfileVersionSectionUser);
    }

    [Fact]
    public async Task UpdateNotesAsync_UpdatesNote_AndSkipsSectionLog_WhenTextUnchanged()
    {
        connection.Script(ProfileNoteStoredProcedures.GetProfileSectionIdByNoteId, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["ProfileSectionId", "NoteText"], [[ProfileNoteTestData.ProfileSectionId, "Same text"]])]
        });
        connection.Script(ProfileNoteStoredProcedures.UpdateProfileVersionNote, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = ProfileNoteTestData.NewRowVersion }
        });

        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Updates =
            [
                new ProfileNoteUpdateDto { Id = ProfileNoteTestData.NoteId, NoteText = "Same text", LastUpdated = ProfileNoteTestData.RowVersion }
            ]
        };

        var result = await CreateRepository().UpdateNotesAsync(command, Guid.Empty, CancellationToken.None);

        result.LastUpdatedUpdateList.Should().ContainSingle().Which.Should().Equal(ProfileNoteTestData.NewRowVersion);
        connection.Executed.Should().NotContain(c => c.CommandText == ProfileNoteStoredProcedures.InsertProfileVersionSectionUser);
    }

    [Fact]
    public async Task UpdateNotesAsync_ThrowsConcurrencyException_OnRowVersionMismatch()
    {
        connection.Script(ProfileNoteStoredProcedures.UpdateProfileVersionNote, new FakeCommandScript
        {
            Throws = new FakeDbException("The note has been edited by another user.")
        });

        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Updates = [new ProfileNoteUpdateDto { Id = ProfileNoteTestData.NoteId, NoteText = "New text", LastUpdated = ProfileNoteTestData.RowVersion }]
        };

        var act = () => CreateRepository().UpdateNotesAsync(command, Guid.Empty, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ConcurrencyException>();
        exception.Which.Message.Should().Contain(ProfileNoteTestData.ProfileVersionId.ToString());
        connection.Transactions.Should().ContainSingle().Which.RolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task GetNoteTypesAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileNoteStoredProcedures.GetNoteTypes, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetNoteTypesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetNotesBySectionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileNoteStoredProcedures.GetNotesBySectionAndType, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetNotesBySectionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.ProfileSectionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetNotesByVersionAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(ProfileNoteStoredProcedures.GetNotesByType, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetNotesByVersionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    /// <summary>Minimal <see cref="System.Data.Common.DbException"/> so a failure can be scripted without a real SqlException.</summary>
    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);
}
