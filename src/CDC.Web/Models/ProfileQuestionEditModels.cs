using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>One profile question as read for editing. Property names match the CDC.Api <c>GET /api/profile-questions/{id}</c> response.</summary>
public sealed record ProfileQuestionEditDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's full display name, which may hold italic markup.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the plain-language name shown to non-technical users, which may hold italic markup.</summary>
    public string NonTechnicalName { get; init; } = string.Empty;

    /// <summary>Gets the question's position within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the guidance text shown to help a user answer the question.</summary>
    public string UserGuidance { get; init; } = string.Empty;

    /// <summary>Gets the row version to send back when saving, so a concurrent edit is detected.</summary>
    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>The changes to one question. Property names match the CDC.Api <c>PUT /api/profile-questions/{id}</c> request.</summary>
/// <param name="Name">The question's new full display name.</param>
/// <param name="NonTechnicalName">The question's new plain-language name.</param>
/// <param name="UserGuidance">The question's new guidance text.</param>
/// <param name="LastUpdated">The row version as last read.</param>
public sealed record UpdateProfileQuestionRequest(string Name, string NonTechnicalName, string UserGuidance, byte[] LastUpdated);

/// <summary>Outcome of reading the content of the guidance reports through CDC.Api.</summary>
public enum ProfileGuidanceContentOutcome
{
    /// <summary>The content was read.</summary>
    Success,

    /// <summary>The user may not get the guidance reports.</summary>
    Forbidden
}

/// <summary>The result of reading the content of the guidance reports.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Content">The content; <see langword="null"/> unless <paramref name="Outcome"/> is <see cref="ProfileGuidanceContentOutcome.Success"/>.</param>
public sealed record ProfileGuidanceContentResult(ProfileGuidanceContentOutcome Outcome, ProfileGuidanceContentDto? Content = null);

/// <summary>Outcome of saving a question through CDC.Api, distinguishing the failure kinds the page reports differently.</summary>
public enum ProfileQuestionUpdateOutcome
{
    /// <summary>The question was saved.</summary>
    Success,

    /// <summary>CDC.Api rejected the submission as invalid.</summary>
    ValidationFailed,

    /// <summary>The question was edited by someone else.</summary>
    Conflict,

    /// <summary>The user may not edit user guidance.</summary>
    Forbidden,

    /// <summary>The question no longer exists.</summary>
    NotFound,

    /// <summary>The save failed for any other reason.</summary>
    Error
}

/// <summary>The result of saving a question.</summary>
public sealed record ProfileQuestionUpdateResult
{
    /// <summary>Gets the outcome.</summary>
    public ProfileQuestionUpdateOutcome Outcome { get; init; }

    /// <summary>Gets the reason the save failed; <see langword="null"/> on success.</summary>
    public string? ErrorMessage { get; init; }
}
