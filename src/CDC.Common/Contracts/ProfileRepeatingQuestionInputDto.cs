namespace CDC.Common.Contracts;

/// <summary>
/// The rows a save posts for one repeating question, as part of <c>POST /api/profile-sections/save</c>. They stand for all the question's
/// rows: a recorded row that is not among them is deleted, a row that is not recorded is added, and the order they are in is the order the
/// question keeps them in. A question the save does not list is left as it is.
/// </summary>
public sealed record ProfileRepeatingQuestionInputDto
{
    /// <summary>Gets the repeating question.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Gets the question's rows, in order.</summary>
    public IReadOnlyList<ProfileQuestionRowInputDto> Rows { get; init; } = [];
}

/// <summary>One row of a repeating question in a save, with its answer to each of the question's fields.</summary>
public sealed record ProfileQuestionRowInputDto
{
    /// <summary>Gets the row identifier: that of a recorded row, or for a row the user added, a new identifier the page made up.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the row's answers. A field that is not among them is left as it is.</summary>
    public IReadOnlyList<ProfileFieldAnswerInputDto> Answers { get; init; } = [];
}
