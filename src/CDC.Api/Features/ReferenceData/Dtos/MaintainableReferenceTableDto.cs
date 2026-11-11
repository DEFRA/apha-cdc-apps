namespace CDC.Api.Features.ReferenceData.Dtos;

/// <summary>A reference table whose values a profile editor can maintain.</summary>
public sealed record MaintainableReferenceTableDto
{
    /// <summary>Gets the reference table identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the table name shown in the dropdown.</summary>
    public string Name { get; init; } = string.Empty;
}
