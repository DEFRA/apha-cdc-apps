using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileNotes;

public class ProfileNoteServiceTests
{
    private readonly Mock<IProfileNoteRepository> repository = new(MockBehavior.Strict);

    private CDC.Api.Features.ProfileNotes.ProfileNoteService CreateService() =>
        new(repository.Object, NullLogger<CDC.Api.Features.ProfileNotes.ProfileNoteService>.Instance);

    [Fact]
    public async Task GetNoteTypes_MapsEachType()
    {
        IReadOnlyList<ProfileNoteType> noteTypes =
        [
            new ProfileNoteType { Id = ProfileNoteTestData.NoteTypeId, Name = "Comment", PluralName = "Comments" }
        ];

        repository.Setup(repo => repo.GetNoteTypesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(noteTypes);

        var result = await CreateService().GetNoteTypesAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Comment");
    }

    [Fact]
    public async Task GetNotesBySection_MapsNotesAndQuestionReferences()
    {
        repository
            .Setup(repo => repo.GetNotesBySectionAsync(
                ProfileNoteTestData.ProfileVersionId,
                ProfileNoteTestData.ProfileSectionId,
                ProfileNoteTestData.NoteTypeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProfileNoteTestData.ProfileNote()]);

        var result = await CreateService().GetNotesBySectionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.ProfileSectionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].QuestionReferences.Should().ContainSingle();
    }

    [Fact]
    public async Task GetNotesByVersion_MapsNotes()
    {
        repository
            .Setup(repo => repo.GetNotesByVersionAsync(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.NoteTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProfileNoteTestData.ProfileNote()]);

        var result = await CreateService().GetNotesByVersionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(ProfileNoteTestData.NoteId);
    }

    [Fact]
    public async Task UpdateNotes_PassesEmptyUserId_AndMapsResult()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
        };
        var repositoryResult = new ProfileNoteChangesetResult([ProfileNoteTestData.NoteId], [ProfileNoteTestData.NewRowVersion], []);

        repository
            .Setup(repo => repo.UpdateNotesAsync(command, Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);

        var result = await CreateService().UpdateNotesAsync(command, CancellationToken.None);

        result.IdInsertList.Should().ContainSingle().Which.Should().Be(ProfileNoteTestData.NoteId);
        repository.VerifyAll();
    }
}
