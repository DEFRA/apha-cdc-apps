namespace CDC.Api.Features.ProfileQuestions.Dtos;

/// <summary>A question within a profile's questionnaire.</summary>
public sealed record ProfileQuestionDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's full display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the question's abbreviated display name.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the plain-language name shown to non-technical users.</summary>
    public string NonTechnicalName { get; init; } = string.Empty;

    /// <summary>Gets the question's position within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the guidance text shown to help a user answer this question.</summary>
    public string UserGuidance { get; init; } = string.Empty;

    /// <summary>Gets the SQL Server <c>rowversion</c> to send back when updating.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
