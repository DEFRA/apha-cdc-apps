namespace CDC.Api.Features.ReferenceData.Dtos;

/// <summary>The kind of change made to a reference value, as the legacy <c>ChangeActionType</c>.</summary>
public enum ReferenceValueChangeAction
{
    /// <summary>Change the lookup text of an active value (<c>spuReferenceValue</c>).</summary>
    Edit = 0,

    /// <summary>Add a value to the table (<c>spiReferenceValue</c>).</summary>
    Add = 1,

    /// <summary>End-date an active value (<c>sppReferenceValue</c>).</summary>
    Inactivate = 2,

    /// <summary>Delete an active value no profile references (<c>spdReferenceValue</c>).</summary>
    Delete = 3
}

/// <summary>A requested change to a value of a maintainable reference table.</summary>
public sealed record ChangeReferenceValueRequestDto
{
    /// <summary>Gets the kind of change.</summary>
    [System.Text.Json.Serialization.JsonRequired]
    public ReferenceValueChangeAction Action { get; init; }

    /// <summary>Gets the value being changed. Not used when adding.</summary>
    public Guid? ValueId { get; init; }

    /// <summary>Gets the new lookup text. Required when editing or adding; ignored otherwise.</summary>
    public string? NewLookupValue { get; init; }

    /// <summary>Gets the reason for the change, recorded in the audit trail.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets the row version read with the value. Required unless adding.</summary>
    public byte[]? LastUpdated { get; init; }
}

/// <summary>The outcome of a successful reference value change.</summary>
public sealed record ChangeReferenceValueResultDto
{
    /// <summary>Gets the value changed, or the identifier given to an added value.</summary>
    public Guid ValueId { get; init; }
}
