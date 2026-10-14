using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileNoteDisplayHelperTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid OtherSectionId = Guid.NewGuid();
    private static readonly Guid FirstQuestionId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid ThirdQuestionId = Guid.NewGuid();

    private static ProfileSectionMetadataDto Section() => new()
    {
        Id = SectionId,
        Name = "Summary",
        ShortName = "SUMMARY",
        SectionNumber = 2,
        Questions =
        [
            new ProfileQuestionMetadataDto { Id = ThirdQuestionId, SectionId = SectionId, ShortName = "Third", QuestionNumber = 3, Fields = [] },
            new ProfileQuestionMetadataDto { Id = FirstQuestionId, SectionId = SectionId, ShortName = "First", QuestionNumber = 1, Fields = [] },
            new ProfileQuestionMetadataDto { Id = SecondQuestionId, SectionId = SectionId, ShortName = "Second", QuestionNumber = 2, Fields = [] }
        ]
    };

    private static QuestionReferenceDto Reference(Guid questionId, Guid sectionId) =>
        new() { ProfileSectionId = sectionId, ProfileQuestionId = questionId };

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("<p>One line</p>", "One line")]
    [InlineData("<p>First</p><p>Second</p>", "FirstSecond")]
    [InlineData("<p class=\"x\">Kept</p>", "<p class=\"x\">Kept")]
    [InlineData("<p>A <a href=\"https://example.com\">link</a></p>", "A <a href=\"https://example.com\">link</a>")]
    public void StripParagraphTags_RemovesOnlyThePlainParagraphTags(string? html, string expected) =>
        Assert.Equal(expected, ProfileNoteDisplayHelper.StripParagraphTags(html));

    [Fact]
    public void ToSlimText_ReturnsShortTextUnchanged_WithItsTagsRemoved() =>
        Assert.Equal("A short note", ProfileNoteDisplayHelper.ToSlimText("<p>A <b>short</b> note</p>"));

    [Fact]
    public void ToSlimText_ReturnsEmpty_ForNoText() =>
        Assert.Equal(string.Empty, ProfileNoteDisplayHelper.ToSlimText(null));

    [Fact]
    public void ToSlimText_KeepsTextOfExactlyFiftyCharacters() =>
        Assert.Equal(new string('x', 50), ProfileNoteDisplayHelper.ToSlimText(new string('x', 50)));

    [Fact]
    public void ToSlimText_ShortensLongerText_ToItsFirstFortySevenCharactersAndAnEllipsis() =>
        Assert.Equal(new string('x', 47) + "...", ProfileNoteDisplayHelper.ToSlimText($"<p>{new string('x', 51)}</p>"));

    [Fact]
    public void BuildQuestionReferenceText_ListsTheSectionsQuestionsInQuestionOrder()
    {
        var note = new ProfileNoteDto
        {
            QuestionReferences = [Reference(ThirdQuestionId, SectionId), Reference(FirstQuestionId, SectionId)]
        };

        Assert.Equal("2.1, 2.3", ProfileNoteDisplayHelper.BuildQuestionReferenceText(note, Section()));
    }

    [Fact]
    public void BuildQuestionReferenceText_IgnoresLinksToOtherSections()
    {
        var note = new ProfileNoteDto
        {
            QuestionReferences = [Reference(FirstQuestionId, OtherSectionId), Reference(Guid.NewGuid(), SectionId)]
        };

        Assert.Equal(string.Empty, ProfileNoteDisplayHelper.BuildQuestionReferenceText(note, Section()));
    }

    [Fact]
    public void BuildQuestionReferenceText_IsEmpty_ForANoteWithNoLinks() =>
        Assert.Equal(string.Empty, ProfileNoteDisplayHelper.BuildQuestionReferenceText(new ProfileNoteDto(), Section()));
}
