namespace CDC.Api.Features.ReferenceData.Dtos;

/// <summary>One selectable value from a generic reference table.</summary>
public sealed record ReferenceValueDto
{
    /// <summary>Gets the reference value identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display text.</summary>
    public string Value { get; init; } = string.Empty;
}
