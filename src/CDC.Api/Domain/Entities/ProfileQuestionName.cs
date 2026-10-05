using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// The display names of one profile question, as recorded for a specific profile version
/// section (<c>spgProfileVersionSection</c>'s "Question Names" result set).
/// </summary>
public sealed record ProfileQuestionName : BaseEntity
{
    /// <summary>Gets the question's full display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the plain-language name shown to non-technical users.</summary>
    public required string NonTechnicalName { get; init; }
}
