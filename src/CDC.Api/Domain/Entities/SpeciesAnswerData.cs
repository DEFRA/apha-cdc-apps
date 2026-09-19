namespace CDC.Api.Domain.Entities;

/// <summary>
/// All recorded answers for a single species, together with the row version used for
/// optimistic concurrency on update.
/// </summary>
public sealed record SpeciesAnswerData
{
    /// <summary>Gets the species the answers belong to.</summary>
    public required Guid SpeciesId { get; init; }

    /// <summary>Gets the species display name.</summary>
    public required string SpeciesName { get; init; }

    /// <summary>
    /// Gets the SQL Server <c>timestamp</c>/<c>rowversion</c> of the species row. It must be
    /// echoed back on update, which fails if another user has saved in the meantime.
    /// </summary>
    public required byte[] LastUpdated { get; init; }

    /// <summary>Gets the answers grouped by questionnaire section, ordered by section number.</summary>
    public required IReadOnlyList<SpeciesSection> Sections { get; init; }
}
