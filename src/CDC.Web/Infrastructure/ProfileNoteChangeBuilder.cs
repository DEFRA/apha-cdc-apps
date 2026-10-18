using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Builds the changes the Further information page sends to CDC.Api, following the legacy <c>EditNoteUI</c> save: each
/// ticked question is linked to the note and each unticked one is unlinked, for the current section only, so links the
/// note has in other sections are untouched.
/// </summary>
public static class ProfileNoteChangeBuilder
{
    private const int RowVersionLength = 8;

    /// <summary>Builds a new note linked to the ticked questions.</summary>
    /// <param name="id">The identifier to give the note.</param>
    /// <param name="noteText">The note's rich-text content.</param>
    /// <param name="sectionId">The section the questions belong to.</param>
    /// <param name="questionIds">The ticked questions.</param>
    /// <returns>The insert.</returns>
    public static ProfileNoteInsert BuildInsert(Guid id, string noteText, Guid sectionId, IEnumerable<Guid> questionIds) =>
        new(id, noteText, [.. questionIds.Distinct().Select(questionId => Reference(sectionId, questionId))]);

    /// <summary>Builds the change that makes a note's links in one section match the ticked questions.</summary>
    /// <param name="note">The note as currently stored.</param>
    /// <param name="lastUpdated">The row version read when the note was opened.</param>
    /// <param name="sectionId">The section the questions belong to.</param>
    /// <param name="tickedQuestionIds">The ticked questions.</param>
    /// <returns>The update, or <see langword="null"/> when the note is already linked to exactly the ticked questions.</returns>
    public static ProfileNoteUpdate? BuildUpdate(ProfileNoteDto note, byte[] lastUpdated, Guid sectionId, IReadOnlyCollection<Guid> tickedQuestionIds)
    {
        var linked = note.QuestionReferences
            .Where(reference => reference.ProfileSectionId == sectionId)
            .Select(reference => reference.ProfileQuestionId)
            .ToHashSet();

        var adds = tickedQuestionIds.Distinct().Where(questionId => !linked.Contains(questionId)).Select(questionId => Reference(sectionId, questionId)).ToList();
        var removes = linked.Where(questionId => !tickedQuestionIds.Contains(questionId)).Select(questionId => Reference(sectionId, questionId)).ToList();

        return adds.Count == 0 && removes.Count == 0
            ? null
            : new ProfileNoteUpdate(note.Id, note.NoteText, lastUpdated, adds, removes);
    }

    /// <summary>Builds the change that unlinks a note from every question in one section (the legacy "Delete" button, which
    /// removes the association and leaves the note in the master list).</summary>
    /// <param name="note">The note as currently stored.</param>
    /// <param name="lastUpdated">The row version read when the table was shown.</param>
    /// <param name="sectionId">The section to unlink the note from.</param>
    /// <returns>The update, or <see langword="null"/> when the note is not linked to any question in the section.</returns>
    public static ProfileNoteUpdate? BuildRemoval(ProfileNoteDto note, byte[] lastUpdated, Guid sectionId) =>
        BuildUpdate(note, lastUpdated, sectionId, []);

    /// <summary>Decodes a row version posted back by a form.</summary>
    /// <param name="value">The Base64 text.</param>
    /// <returns>The 8-byte row version, or <see langword="null"/> when the text is not one.</returns>
    public static byte[]? DecodeRowVersion(string? value)
    {
        Span<byte> buffer = stackalloc byte[RowVersionLength];

        return Convert.TryFromBase64String(value ?? string.Empty, buffer, out var written) && written == RowVersionLength ? buffer.ToArray() : null;
    }

    private static QuestionReferenceDto Reference(Guid sectionId, Guid questionId) =>
        new() { ProfileSectionId = sectionId, ProfileQuestionId = questionId };
}
