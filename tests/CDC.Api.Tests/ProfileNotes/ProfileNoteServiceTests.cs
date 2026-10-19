using CDC.Api.Application;
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
    private static readonly Guid AuditUserId = Guid.Parse("8a3c0e55-4d2f-4f6a-9d5b-0b4c2f7a1e10");

    private readonly Mock<IProfileNoteRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IUserContext> userContext = new();

    public ProfileNoteServiceTests() =>
        userContext.SetupGet(context => context.UserId).Returns(AuditUserId);

    private CDC.Api.Features.ProfileNotes.ProfileNoteService CreateService() =>
        new(repository.Object, userContext.Object, NullLogger<CDC.Api.Features.ProfileNotes.ProfileNoteService>.Instance);

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
    public async Task UpdateNotes_PassesTheActingUser_AndMapsResult()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
        };
        var repositoryResult = new ProfileNoteChangesetResult([ProfileNoteTestData.NoteId], [ProfileNoteTestData.NewRowVersion], []);

        repository
            .Setup(repo => repo.UpdateNotesAsync(command, AuditUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);

        var result = await CreateService().UpdateNotesAsync(command, CancellationToken.None);

        result.IdInsertList.Should().ContainSingle().Which.Should().Be(ProfileNoteTestData.NoteId);
        repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateNotes_Throws_WhenTheRequestHasNoKnownUser()
    {
        userContext.SetupGet(context => context.UserId).Returns((Guid?)null);
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
        };

        var act = async () => await CreateService().UpdateNotesAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }
}
