using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileNotes.Dtos;

namespace CDC.Api.Tests.ProfileNotes;

/// <summary>
/// Builders for profile note domain objects and DTOs. The entities use <c>required</c>
/// members, which AutoFixture cannot populate, so they are constructed explicitly here.
/// </summary>
internal static class ProfileNoteTestData
{
    public static readonly Guid ProfileVersionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProfileSectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid NoteTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid NoteId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ProfileQuestionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static byte[] RowVersion => [0, 0, 0, 0, 0, 0, 7, 209];

    public static byte[] NewRowVersion => [0, 0, 0, 0, 0, 0, 7, 210];

    public static QuestionReference QuestionReference() => new()
    {
        ProfileSectionId = ProfileSectionId,
        ProfileQuestionId = ProfileQuestionId
    };

    public static ProfileNote ProfileNote() => new()
    {
        Id = NoteId,
        NoteText = "A note",
        LastUpdated = RowVersion,
        QuestionReferences = [QuestionReference()]
    };

    public static ProfileNoteDto ProfileNoteDto() => new()
    {
        Id = NoteId,
        NoteText = "A note",
        LastUpdated = RowVersion
    };
}
