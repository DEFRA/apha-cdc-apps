using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Discriminates which value a <see cref="SpeciesFieldValueChange"/> carries. Replaces the
/// legacy <c>ParameterName</c> string ("@BooleanValue", "@ListValue", ...) with a typed value,
/// while still driving the same <c>spuSpeciesFieldValue</c> parameters.
/// </summary>
public enum SpeciesFieldValueKind
{
    /// <summary>Clears the stored answer: the stored procedure deletes the existing row.</summary>
    None = 0,

    /// <summary>Sets <c>@BooleanValue</c>.</summary>
    Boolean = 1,

    /// <summary>Sets <c>@ListValue</c> to a single reference data item.</summary>
    List = 2,

    /// <summary>Sets <c>@TextValue</c>.</summary>
    Text = 3,

    /// <summary>Replaces all rows for a multi-select list field.</summary>
    MultiValue = 4
}

/// <summary>
/// A single field-level change within an answer data update.
/// </summary>
public sealed record SpeciesFieldValueChange
{
    /// <summary>Gets the species field being answered.</summary>
    public Guid FieldId { get; init; }

    /// <summary>Gets the kind of value being supplied.</summary>
    public SpeciesFieldValueKind Kind { get; init; }

    /// <summary>Gets the answer when <see cref="Kind"/> is <see cref="SpeciesFieldValueKind.Boolean"/>.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the answer when <see cref="Kind"/> is <see cref="SpeciesFieldValueKind.List"/>.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer when <see cref="Kind"/> is <see cref="SpeciesFieldValueKind.Text"/>.</summary>
    public string? TextValue { get; init; }

    /// <summary>
    /// Gets the answers when <see cref="Kind"/> is <see cref="SpeciesFieldValueKind.MultiValue"/>.
    /// The supplied list replaces every stored value for the field; an empty list clears it.
    /// </summary>
    public IReadOnlyList<Guid> MultiValues { get; init; } = [];
}

/// <summary>
/// Applies a set of answer changes to one species, transactionally, and returns the new row
/// version.
/// </summary>
public sealed record UpdateSpeciesAnswerDataCommand : IRequest<Result<UpdateSpeciesAnswerDataResultDto>>
{
    /// <summary>Gets the species being updated.</summary>
    public Guid SpeciesId { get; init; }

    /// <summary>
    /// Gets the row version last read for this species. The update is rejected with HTTP 409
    /// if it no longer matches the stored value.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the field-level changes to apply. Must contain at least one entry.</summary>
    public IReadOnlyList<SpeciesFieldValueChange> Changes { get; init; } = [];
}
