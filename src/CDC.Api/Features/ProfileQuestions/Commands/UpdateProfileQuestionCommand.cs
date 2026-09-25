using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileQuestions.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileQuestions.Commands;

/// <summary>
/// Updates a question's guidance text and display names. Mirrors the legacy
/// <c>UpdateProfileQuestionRequest</c> data contract.
/// </summary>
/// <remarks>
/// <see cref="ShortName"/> and <c>QuestionNumber</c> are deliberately not part of this command:
/// the legacy <c>spuProfileQuestion</c> call never passed them either, so they were never
/// actually persisted despite appearing on the legacy WCF request.
/// </remarks>
public sealed record UpdateProfileQuestionCommand : IRequest<Result<ProfileQuestionDto>>
{
    /// <summary>Gets the question being updated.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's new full display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the question's new plain-language name.</summary>
    public string NonTechnicalName { get; init; } = string.Empty;

    /// <summary>Gets the question's new guidance text.</summary>
    public string UserGuidance { get; init; } = string.Empty;

    /// <summary>
    /// Gets the row version read alongside the question, so a concurrent edit can be detected.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];
}
