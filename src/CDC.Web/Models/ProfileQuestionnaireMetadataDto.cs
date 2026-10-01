namespace CDC.Web.Models;

/// <summary>
/// The profile questionnaire structure, as returned by <c>GET /api/profile-sections/metadata</c>
/// on CDC.Api. Field names and types mirror the API's <c>ProfileQuestionnaireMetadataDto</c>
/// exactly. The 16 fixed profile reference sections are read directly from this structure
/// rather than hardcoded, so section names/ids always match the database.
/// </summary>
public sealed record ProfileQuestionnaireMetadataDto
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public IReadOnlyList<ProfileSectionMetadataDto> Sections { get; init; } = [];
}

/// <summary>A profile reference section (for example "Epidemiology").</summary>
public sealed record ProfileSectionMetadataDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public int SectionNumber { get; init; }

    /// <summary>Gets the questions in this section, ordered by question number.</summary>
    public IReadOnlyList<ProfileQuestionMetadataDto> Questions { get; init; } = [];
}

/// <summary>A question within a profile reference section.</summary>
public sealed record ProfileQuestionMetadataDto
{
    public Guid Id { get; init; }

    public Guid SectionId { get; init; }

    public string ShortName { get; init; } = string.Empty;

    public int QuestionNumber { get; init; }

    public bool IsPerSpecies { get; init; }

    public bool IsRepeating { get; init; }

    /// <summary>Gets the fields that make up the answer, ordered by field number.</summary>
    public IReadOnlyList<ProfileFieldMetadataDto> Fields { get; init; } = [];
}

/// <summary>A single answerable field within a profile question.</summary>
public sealed record ProfileFieldMetadataDto
{
    public Guid Id { get; init; }

    public Guid QuestionId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public int FieldNumber { get; init; }

    public Guid DataFieldTypeId { get; init; }

    /// <summary>
    /// Gets the data type name, for example <c>Boolean</c>, <c>List</c>, <c>MultiValueList</c>,
    /// <c>Text</c>, <c>Long Text</c> (rich/HTML content), <c>Decimal</c> or <c>Date</c>.
    /// </summary>
    public string DataTypeName { get; init; } = string.Empty;

    public bool IsMandatory { get; init; }

    public Guid ReferenceTableId { get; init; }

    public bool ReferenceTableIsMaintainable { get; init; }
}
