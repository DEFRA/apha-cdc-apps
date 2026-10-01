namespace CDC.Web.Models;

/// <summary>
/// All recorded answers for a single species, as returned by
/// <c>GET /api/species/{speciesId}/answers</c> on CDC.Api. Field names and types mirror the
/// API's <c>SpeciesAnswerDataDto</c> exactly.
/// </summary>
public sealed record SpeciesAnswerDataDto
{
    public Guid SpeciesId { get; init; }

    public string SpeciesName { get; init; } = string.Empty;

    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the answers grouped by questionnaire section.</summary>
    public IReadOnlyList<SpeciesSectionDto> Sections { get; init; } = [];
}

/// <summary>The answered field values for one questionnaire section.</summary>
public sealed record SpeciesSectionDto
{
    public Guid SectionId { get; init; }

    public IReadOnlyList<SpeciesFieldValueDto> FieldValues { get; init; } = [];
}

/// <summary>One stored answer. Exactly one value property is populated, per the field's data type.</summary>
public sealed record SpeciesFieldValueDto
{
    public Guid Id { get; init; }

    public Guid QuestionId { get; init; }

    public int FieldNumber { get; init; }

    public bool? BooleanValue { get; init; }

    public Guid? ListValue { get; init; }

    public string? TextValue { get; init; }
}
