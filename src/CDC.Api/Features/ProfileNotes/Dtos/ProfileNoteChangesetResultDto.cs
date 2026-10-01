namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>
/// Outcome of applying a note changeset, mirroring the legacy <c>ChangeResult</c> data
/// contract. Each list is parallel to the corresponding request list: entry <c>n</c> of
/// <see cref="IdInsertList"/>/<see cref="LastUpdatedInsertList"/> corresponds to entry
/// <c>n</c> of the request's <c>inserts</c>, and <see cref="LastUpdatedUpdateList"/> entry
/// <c>n</c> corresponds to entry <c>n</c> of the request's <c>updates</c>.
/// </summary>
public sealed record ProfileNoteChangesetResultDto
{
    /// <summary>Gets the identifiers assigned to the newly inserted notes.</summary>
    public IReadOnlyList<Guid> IdInsertList { get; init; } = [];

    /// <summary>Gets the row versions created for the newly inserted notes.</summary>
    public IReadOnlyList<byte[]> LastUpdatedInsertList { get; init; } = [];

    /// <summary>Gets the row versions created for the updated notes.</summary>
    public IReadOnlyList<byte[]> LastUpdatedUpdateList { get; init; } = [];
}
