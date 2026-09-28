namespace CDC.Api.Features.ProfileQuestions.Dtos;

/// <summary>Summary information about a question, used to populate question pickers.</summary>
public sealed record ProfileQuestionInfoDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's full display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the question's position within its section.</summary>
    public int QuestionNumber { get; init; }
}
