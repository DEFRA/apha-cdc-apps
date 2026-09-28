using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A question within a profile's questionnaire, including its user guidance text. Mirrors the
/// legacy <c>ProfileQuestionData</c> data contract.
/// </summary>
public sealed record ProfileQuestion : BaseEntity
{
    /// <summary>Gets the question's full display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the question's abbreviated display name.</summary>
    public required string ShortName { get; init; }

    /// <summary>Gets the plain-language name shown to non-technical users.</summary>
    public required string NonTechnicalName { get; init; }

    /// <summary>Gets the question's position within its section.</summary>
    public required int QuestionNumber { get; init; }

    /// <summary>Gets the guidance text shown to help a user answer this question.</summary>
    public required string UserGuidance { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> used for optimistic concurrency.</summary>
    public required byte[] LastUpdated { get; init; }
}
