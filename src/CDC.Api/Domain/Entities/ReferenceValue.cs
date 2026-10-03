namespace CDC.Api.Domain.Entities;

/// <summary>
/// One selectable value from a generic reference table, sourced from
/// <c>spgReferenceValueByTable</c>. Backs "List" type question fields across the application,
/// including the species questionnaire.
/// </summary>
public sealed record ReferenceValue
{
    /// <summary>Gets the reference value identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display text.</summary>
    public required string Value { get; init; }
}
