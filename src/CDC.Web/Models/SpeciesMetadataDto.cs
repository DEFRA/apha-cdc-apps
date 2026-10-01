namespace CDC.Web.Models;

/// <summary>
/// The species questionnaire structure, as returned by <c>GET /api/species/metadata</c> on
/// CDC.Api. Field names and types mirror the API's <c>SpeciesMetadataDto</c> exactly.
/// </summary>
public sealed record SpeciesMetadataDto
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public IReadOnlyList<SpeciesSectionMetadataDto> Sections { get; init; } = [];
}

/// <summary>A section of the species questionnaire.</summary>
public sealed record SpeciesSectionMetadataDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public int SectionNumber { get; init; }

    /// <summary>Gets the questions in this section, ordered by question number.</summary>
    public IReadOnlyList<SpeciesQuestionMetadataDto> Questions { get; init; } = [];
}

/// <summary>A question within a questionnaire section.</summary>
public sealed record SpeciesQuestionMetadataDto
{
    public Guid Id { get; init; }

    public Guid SectionId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public int QuestionNumber { get; init; }

    /// <summary>Gets the fields that make up the answer, ordered by field number.</summary>
    public IReadOnlyList<SpeciesFieldMetadataDto> Fields { get; init; } = [];
}

/// <summary>A single answerable field within a question.</summary>
public sealed record SpeciesFieldMetadataDto
{
    public Guid Id { get; init; }

    public Guid QuestionId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public int FieldNumber { get; init; }

    public Guid DataFieldTypeId { get; init; }

    public string DataTypeName { get; init; } = string.Empty;

    public bool IsMandatory { get; init; }

    public Guid ReferenceTableId { get; init; }

    public bool ReferenceTableIsMaintainable { get; init; }
}
