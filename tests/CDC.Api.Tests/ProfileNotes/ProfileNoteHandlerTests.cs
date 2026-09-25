using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using CDC.Api.Features.ProfileNotes.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileNotes;

public class ProfileNoteHandlerTests
{
    private readonly Mock<IProfileNoteService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetNoteTypesQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileNoteTypeDto> noteTypes = [new ProfileNoteTypeDto { Id = ProfileNoteTestData.NoteTypeId, Name = "Comment" }];

        service.Setup(svc => svc.GetNoteTypesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(noteTypes);

        var result = await new GetNoteTypesQueryHandler(service.Object).Handle(new GetNoteTypesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(noteTypes);
    }

    [Fact]
    public async Task GetNotesBySectionQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileNoteDto> notes = [ProfileNoteTestData.ProfileNoteDto()];

        service
            .Setup(svc => svc.GetNotesBySectionAsync(
                ProfileNoteTestData.ProfileVersionId,
                ProfileNoteTestData.ProfileSectionId,
                ProfileNoteTestData.NoteTypeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notes);

        var result = await new GetNotesBySectionQueryHandler(service.Object).Handle(
            new GetNotesBySectionQuery(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.ProfileSectionId, ProfileNoteTestData.NoteTypeId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(notes);
    }

    [Fact]
    public async Task GetNotesByVersionQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileNoteDto> notes = [ProfileNoteTestData.ProfileNoteDto()];

        service
            .Setup(svc => svc.GetNotesByVersionAsync(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.NoteTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notes);

        var result = await new GetNotesByVersionQueryHandler(service.Object).Handle(
            new GetNotesByVersionQuery(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.NoteTypeId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(notes);
    }

    [Fact]
    public async Task UpdateNotesCommandHandler_ReturnsSuccess()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
        };
        var resultDto = new ProfileNoteChangesetResultDto
        {
            IdInsertList = [ProfileNoteTestData.NoteId],
            LastUpdatedInsertList = [ProfileNoteTestData.NewRowVersion]
        };

        service.Setup(svc => svc.UpdateNotesAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(resultDto);

        var result = await new UpdateNotesCommandHandler(service.Object, NullLogger<UpdateNotesCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task UpdateNotesCommandHandler_ReturnsConflict_OnConcurrencyException()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Deletes = [new ProfileNoteDeleteDto { Id = ProfileNoteTestData.NoteId, LastUpdated = ProfileNoteTestData.RowVersion }]
        };

        service
            .Setup(svc => svc.UpdateNotesAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await new UpdateNotesCommandHandler(service.Object, NullLogger<UpdateNotesCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }
}
