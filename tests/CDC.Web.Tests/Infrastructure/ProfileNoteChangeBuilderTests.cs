using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileNoteChangeBuilderTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid OtherSectionId = Guid.NewGuid();
    private static readonly Guid FirstQuestionId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid ThirdQuestionId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private static QuestionReferenceDto Reference(Guid questionId, Guid? sectionId = null) =>
        new() { ProfileSectionId = sectionId ?? SectionId, ProfileQuestionId = questionId };

    private static ProfileNoteDto NoteLinkedTo(params QuestionReferenceDto[] references) =>
        new() { Id = Guid.NewGuid(), NoteText = "<p>A note</p>", LastUpdated = RowVersion, QuestionReferences = references };

    [Fact]
    public void BuildInsert_LinksTheNewNoteToEachDistinctTickedQuestionOfTheSection()
    {
        var id = Guid.NewGuid();

        var insert = ProfileNoteChangeBuilder.BuildInsert(id, "<p>New</p>", SectionId, [FirstQuestionId, SecondQuestionId, FirstQuestionId]);

        Assert.Equal(id, insert.Id);
        Assert.Equal("<p>New</p>", insert.NoteText);
        Assert.Equal([FirstQuestionId, SecondQuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.All(insert.QuestionReferenceAdds, reference => Assert.Equal(SectionId, reference.ProfileSectionId));
    }

    [Fact]
    public void BuildUpdate_AddsTheNewlyTickedQuestions_AndRemovesTheUntickedOnes()
    {
        var note = NoteLinkedTo(Reference(FirstQuestionId), Reference(SecondQuestionId));

        var update = ProfileNoteChangeBuilder.BuildUpdate(note, RowVersion, SectionId, [SecondQuestionId, ThirdQuestionId]);

        Assert.NotNull(update);
        Assert.Equal(note.Id, update.Id);
        Assert.Equal("<p>A note</p>", update.NoteText);
        Assert.Equal(RowVersion, update.LastUpdated);
        Assert.Equal([ThirdQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.Equal([FirstQuestionId], update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId));
        Assert.All(update.QuestionReferenceAdds.Concat(update.QuestionReferenceRemoves), reference => Assert.Equal(SectionId, reference.ProfileSectionId));
    }

    [Fact]
    public void BuildUpdate_IgnoresTheLinksTheNoteHasInOtherSections()
    {
        var note = NoteLinkedTo(Reference(FirstQuestionId), Reference(ThirdQuestionId, OtherSectionId));

        var update = ProfileNoteChangeBuilder.BuildUpdate(note, RowVersion, SectionId, [FirstQuestionId, SecondQuestionId]);

        Assert.NotNull(update);
        Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.Empty(update.QuestionReferenceRemoves);
    }

    [Fact]
    public void BuildUpdate_TreatsAQuestionOfAnotherSectionWithTheSameIdAsNotLinkedHere()
    {
        var note = NoteLinkedTo(Reference(FirstQuestionId, OtherSectionId));

        var update = ProfileNoteChangeBuilder.BuildUpdate(note, RowVersion, SectionId, [FirstQuestionId]);

        Assert.NotNull(update);
        Assert.Equal([FirstQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
    }

    [Fact]
    public void BuildUpdate_ReturnsNull_WhenTheTicksAlreadyMatchTheLinks()
    {
        var note = NoteLinkedTo(Reference(FirstQuestionId), Reference(SecondQuestionId));

        Assert.Null(ProfileNoteChangeBuilder.BuildUpdate(note, RowVersion, SectionId, [SecondQuestionId, FirstQuestionId]));
    }

    [Fact]
    public void BuildUpdate_AddsAQuestionOnlyOnce_WhenItIsTickedTwice()
    {
        var note = NoteLinkedTo();

        var update = ProfileNoteChangeBuilder.BuildUpdate(note, RowVersion, SectionId, [FirstQuestionId, FirstQuestionId]);

        Assert.NotNull(update);
        Assert.Equal([FirstQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
    }

    [Fact]
    public void BuildRemoval_UnlinksTheNoteFromEveryQuestionOfTheSectionOnly()
    {
        var note = NoteLinkedTo(Reference(FirstQuestionId), Reference(SecondQuestionId), Reference(ThirdQuestionId, OtherSectionId));

        var update = ProfileNoteChangeBuilder.BuildRemoval(note, RowVersion, SectionId);

        Assert.NotNull(update);
        Assert.Empty(update.QuestionReferenceAdds);
        Assert.Equal(
            new[] { FirstQuestionId, SecondQuestionId }.Order(),
            update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId).Order());
    }

    [Fact]
    public void BuildRemoval_ReturnsNull_WhenTheNoteHasNoLinksInTheSection()
    {
        var note = NoteLinkedTo(Reference(ThirdQuestionId, OtherSectionId));

        Assert.Null(ProfileNoteChangeBuilder.BuildRemoval(note, RowVersion, SectionId));
    }

    [Fact]
    public void DecodeRowVersion_ReturnsTheEightBytes_OfAValidBase64Value() =>
        Assert.Equal(RowVersion, ProfileNoteChangeBuilder.DecodeRowVersion(Convert.ToBase64String(RowVersion)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64!")]
    [InlineData("AQID")]
    [InlineData("AQIDBAUGBwgJCg==")]
    public void DecodeRowVersion_ReturnsNull_WhenTheValueIsNotAnEightByteRowVersion(string? value) =>
        Assert.Null(ProfileNoteChangeBuilder.DecodeRowVersion(value));
}
