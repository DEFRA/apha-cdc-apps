namespace CDC.Web.Models;

/// <summary>Summary information about a question, used to list the questions a note can be linked to.</summary>
public sealed record ProfileQuestionInfoDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's full display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the question's position within its section.</summary>
    public int QuestionNumber { get; init; }
}

/// <summary>A new note to add, with the questions it is linked to. Property names match the CDC.Api <c>PUT /api/profile-notes</c> request.</summary>
/// <param name="Id">The identifier to give the new note.</param>
/// <param name="NoteText">The note's rich-text content.</param>
/// <param name="QuestionReferenceAdds">The questions to link the note to.</param>
public sealed record ProfileNoteInsert(Guid Id, string NoteText, IReadOnlyList<QuestionReferenceDto> QuestionReferenceAdds);

/// <summary>A change to which questions an existing note is linked to. Property names match the CDC.Api <c>PUT /api/profile-notes</c> request.</summary>
/// <param name="Id">The note being changed.</param>
/// <param name="NoteText">The note's content, which is not edited here.</param>
/// <param name="LastUpdated">The note's row version as last read, so a concurrent edit is detected.</param>
/// <param name="QuestionReferenceAdds">The questions to link the note to.</param>
/// <param name="QuestionReferenceRemoves">The questions to unlink the note from.</param>
public sealed record ProfileNoteUpdate(
    Guid Id,
    string NoteText,
    byte[] LastUpdated,
    IReadOnlyList<QuestionReferenceDto> QuestionReferenceAdds,
    IReadOnlyList<QuestionReferenceDto> QuestionReferenceRemoves);

/// <summary>Outcome of changing notes through CDC.Api, distinguishing the failure kinds the page reports differently.</summary>
public enum ProfileNoteChangeOutcome
{
    /// <summary>The change was applied.</summary>
    Success,

    /// <summary>CDC.Api rejected the submission as invalid.</summary>
    ValidationFailed,

    /// <summary>The note was edited by someone else.</summary>
    Conflict,

    /// <summary>The change failed for any other reason.</summary>
    Error
}

/// <summary>Result of changing notes.</summary>
public sealed record ProfileNoteChangeResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required ProfileNoteChangeOutcome Outcome { get; init; }

    /// <summary>Gets the reason to show the user when the outcome is not <see cref="ProfileNoteChangeOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
