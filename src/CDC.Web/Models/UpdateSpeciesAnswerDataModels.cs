namespace CDC.Web.Models;

/// <summary>Discriminates which value a <see cref="SpeciesFieldValueChangeDto"/> carries. Mirrors
/// CDC.Api's <c>SpeciesFieldValueKind</c>.</summary>
public enum SpeciesFieldValueKind
{
    /// <summary>Clears the stored answer.</summary>
    None = 0,

    /// <summary>Sets the boolean value.</summary>
    Boolean = 1,

    /// <summary>Sets a single reference data item.</summary>
    List = 2,

    /// <summary>Sets the text value.</summary>
    Text = 3,

    /// <summary>Replaces all rows for a multi-select list field.</summary>
    MultiValue = 4
}

/// <summary>A single field-level change, for <c>PUT /api/species/answers</c>.</summary>
public sealed record SpeciesFieldValueChangeDto
{
    public Guid FieldId { get; init; }

    public SpeciesFieldValueKind Kind { get; init; }

    public bool? BooleanValue { get; init; }

    public Guid? ListValue { get; init; }

    public string? TextValue { get; init; }

    public IReadOnlyList<Guid> MultiValues { get; init; } = [];
}

/// <summary>Wire contract for <c>PUT /api/species/answers</c> on CDC.Api.</summary>
public sealed record UpdateSpeciesAnswerDataRequestDto
{
    public Guid SpeciesId { get; init; }

    public byte[] LastUpdated { get; init; } = [];

    public IReadOnlyList<SpeciesFieldValueChangeDto> Changes { get; init; } = [];
}

/// <summary>Wire shape of a successful <c>PUT /api/species/answers</c> response body.</summary>
public sealed record UpdateSpeciesAnswerDataResultDto
{
    public Guid SpeciesId { get; init; }

    public byte[] LastUpdated { get; init; } = [];
}

/// <summary>Result of a call to <c>PUT /api/species/answers</c>.</summary>
public sealed record UpdateSpeciesAnswerDataResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required SpeciesUpdateOutcome Outcome { get; init; }

    /// <summary>Gets the new row version on success.</summary>
    public byte[]? LastUpdated { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="SpeciesUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
