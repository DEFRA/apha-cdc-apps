namespace CDC.Common.Contracts;

/// <summary>
/// The result of clearing a profile version section's recorded answers, as returned by
/// <c>POST /api/profile-sections/clear</c>.
/// </summary>
public sealed record ClearProfileSectionResultDto
{
    /// <summary>Gets the number of field values cleared.</summary>
    public int FieldsClearedCount { get; init; }
}
