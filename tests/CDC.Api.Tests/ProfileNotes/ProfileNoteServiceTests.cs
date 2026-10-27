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
    private static readonly Guid ProfileId = Guid.Parse("c1d2e3f4-0000-4000-8000-000000000001");

    private readonly Mock<IProfileNoteRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IUserContext> userContext = new();
    private readonly Mock<IProfileVersionGuard> versions = new();

    public ProfileNoteServiceTests()
    {
        userContext.SetupGet(context => context.UserId).Returns(AuditUserId);
        userContext.SetupGet(context => context.IsProfileEditor).Returns(true);
        GivenVersion(isPublished: false, isCurrent: true);
    }

    private CDC.Api.Features.ProfileNotes.ProfileNoteService CreateService() =>
        new(repository.Object, userContext.Object, versions.Object, NullLogger<CDC.Api.Features.ProfileNotes.ProfileNoteService>.Instance);

    private void GivenVersion(bool isPublished, bool isCurrent) =>
        versions
            .Setup(guard => guard.RequireAsync(ProfileNoteTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionFacts(ProfileId, isPublished, isCurrent));

    private void GivenAnExternalUser(bool isContributor)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(false);
        userContext
            .Setup(context => context.GetProfileRole(ProfileId))
            .Returns(new ProfileRoleAssignment(ProfileId, Guid.NewGuid(), isContributor ? "Technical author" : "Technical reviewer", isContributor));
    }

    private static UpdateNotesCommand InsertCommand() => new()
    {
        ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
        NoteTypeId = ProfileNoteTestData.NoteTypeId,
        Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
    };

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

        var act = async () => await CreateService().UpdateNotesAsync(InsertCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateNotes_AllowsAnAuthorOfTheProfile()
    {
        GivenAnExternalUser(isContributor: true);
        var command = InsertCommand();

        repository
            .Setup(repo => repo.UpdateNotesAsync(command, AuditUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileNoteChangesetResult([ProfileNoteTestData.NoteId], [ProfileNoteTestData.NewRowVersion], []));

        var result = await CreateService().UpdateNotesAsync(command, CancellationToken.None);

        result.IdInsertList.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateNotes_Throws_ForAReviewerOfTheProfile()
    {
        GivenAnExternalUser(isContributor: false);

        var act = async () => await CreateService().UpdateNotesAsync(InsertCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateNotes_Throws_ForAUserWithNoRoleOnTheProfile()
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(false);

        var act = async () => await CreateService().UpdateNotesAsync(InsertCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task UpdateNotes_Throws_WhenTheVersionIsNotTheCurrentDraft(bool isPublished, bool isCurrent)
    {
        GivenVersion(isPublished, isCurrent);

        var act = async () => await CreateService().UpdateNotesAsync(InsertCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotesBySection_Throws_WhenTheUserMayNotViewTheNotes()
    {
        versions
            .Setup(guard => guard.EnsureCanViewNotesAsync(ProfileNoteTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var act = async () => await CreateService().GetNotesBySectionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.ProfileSectionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotesByVersion_Throws_WhenTheUserMayNotViewTheNotes()
    {
        versions
            .Setup(guard => guard.EnsureCanViewNotesAsync(ProfileNoteTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var act = async () => await CreateService().GetNotesByVersionAsync(
            ProfileNoteTestData.ProfileVersionId,
            ProfileNoteTestData.NoteTypeId,
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.VerifyNoOtherCalls();
    }
}
