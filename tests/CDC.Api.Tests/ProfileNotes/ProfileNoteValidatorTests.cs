using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Queries;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileNotes;

public class ProfileNoteValidatorTests
{
    [Fact]
    public void GetNotesBySectionQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetNotesBySectionQueryValidator().Validate(
            new GetNotesBySectionQuery(Guid.Empty, ProfileNoteTestData.ProfileSectionId, ProfileNoteTestData.NoteTypeId));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetNotesBySectionQueryValidator_EmptyProfileSectionId_ShouldFail()
    {
        var result = new GetNotesBySectionQueryValidator().Validate(
            new GetNotesBySectionQuery(ProfileNoteTestData.ProfileVersionId, Guid.Empty, ProfileNoteTestData.NoteTypeId));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetNotesBySectionQueryValidator_InvalidNoteType_ShouldFail()
    {
        var result = new GetNotesBySectionQueryValidator().Validate(
            new GetNotesBySectionQuery(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.ProfileSectionId, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetNotesBySectionQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetNotesBySectionQueryValidator().Validate(
            new GetNotesBySectionQuery(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.ProfileSectionId, ProfileNoteTestData.NoteTypeId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetNotesByVersionQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetNotesByVersionQueryValidator().Validate(new GetNotesByVersionQuery(Guid.Empty, ProfileNoteTestData.NoteTypeId));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetNotesByVersionQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetNotesByVersionQueryValidator().Validate(
            new GetNotesByVersionQuery(ProfileNoteTestData.ProfileVersionId, ProfileNoteTestData.NoteTypeId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateNotesCommandValidator_EmptyChangeset_ShouldFail()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId
        };

        var result = new UpdateNotesCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateNotesCommandValidator_DuplicateChange_ShouldFail()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }],
            Deletes = [new ProfileNoteDeleteDto { Id = ProfileNoteTestData.NoteId, LastUpdated = ProfileNoteTestData.RowVersion }]
        };

        var result = new UpdateNotesCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateNotesCommandValidator_ValidRequest_ShouldPass()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Inserts = [new ProfileNoteInsertDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note" }]
        };

        var result = new UpdateNotesCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateNotesCommandValidator_UpdateWithMissingRowVersion_ShouldFail()
    {
        var command = new UpdateNotesCommand
        {
            ProfileVersionId = ProfileNoteTestData.ProfileVersionId,
            NoteTypeId = ProfileNoteTestData.NoteTypeId,
            Updates = [new ProfileNoteUpdateDto { Id = ProfileNoteTestData.NoteId, NoteText = "Note", LastUpdated = [1, 2, 3] }]
        };

        var result = new UpdateNotesCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
