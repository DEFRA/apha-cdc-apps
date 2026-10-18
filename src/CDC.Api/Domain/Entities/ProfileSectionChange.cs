namespace CDC.Api.Domain.Entities;

/// <summary>
/// Everything one save or clear writes to a profile version section. The repository applies it in a single
/// transaction, as the legacy <c>ProfileVersionSection.Save()</c> did: the section row first (which is what the
/// concurrency check is made against), then the repeating question rows, then the field values.
/// </summary>
public sealed record ProfileSectionChange
{
    /// <summary>Gets the answers to write.</summary>
    public IReadOnlyList<ProfileFieldSaveInstruction> Saves { get; init; } = [];

    /// <summary>Gets the recorded values to remove.</summary>
    public IReadOnlyList<ProfileFieldClearInstruction> Clears { get; init; } = [];

    /// <summary>Gets the repeating question rows to delete, each together with the values it holds.</summary>
    public IReadOnlyList<Guid> QuestionRowIdsToDelete { get; init; } = [];

    /// <summary>Gets a value indicating whether the section's review frequencies, next review dates and review statuses are
    /// blanked, as the legacy <c>ProfileVersionSection.Clear()</c> did. Otherwise they are written back unchanged.</summary>
    public bool ClearRevision { get; init; }

    /// <summary>Gets the row version of the section the caller last read, or <see langword="null"/> to check against the row
    /// as it is when the change is applied. The change is refused when someone else has saved the section since.</summary>
    public byte[]? ExpectedLastUpdated { get; init; }

    /// <summary>Gets the number of recorded values the change adds, replaces or removes.</summary>
    public int ValueChangeCount => Saves.Count + Clears.Count + QuestionRowIdsToDelete.Count;
}
