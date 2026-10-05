namespace CDC.Web.Models;

/// <summary>
/// One selectable value from a generic reference table, as returned by
/// <c>GET /api/reference-data/{referenceTableId}/values</c> on CDC.Api.
/// </summary>
public sealed record ReferenceValueDto
{
    public Guid Id { get; init; }

    public string Value { get; init; } = string.Empty;
}
