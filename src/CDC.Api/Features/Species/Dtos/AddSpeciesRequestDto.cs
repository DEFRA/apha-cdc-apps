namespace CDC.Api.Features.Species.Dtos;

/// <summary>
/// Request body for <c>POST /api/species</c>. The acting user is taken from server
/// configuration rather than this body, so the audit trail cannot be spoofed.
/// </summary>
public sealed record AddSpeciesRequestDto
{
    /// <summary>Gets the display name of the new species.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the chosen parent. Omit or send <see langword="null"/> only when no choice was
    /// made - that is rejected. Send <see cref="Guid.Empty"/> for a root species.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the reason given for the change. Mandatory.</summary>
    public string Reason { get; init; } = string.Empty;
}
