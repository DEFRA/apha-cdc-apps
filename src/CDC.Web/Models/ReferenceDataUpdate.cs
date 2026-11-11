namespace CDC.Web.Models;

/// A requested change to a reference value, carrying the audit metadata spuReferenceValue needs.
/// The effective date is set by the system at the point of change, as it is in the legacy procedure.
/// <see cref="ValueId"/> is ignored when <see cref="Action"/> is <see cref="ReferenceDataChangeAction.Add"/>.
public sealed record ReferenceDataUpdate(
    Guid ReferenceTableId,
    Guid ValueId,
    string NewLookupValue,
    string Reason,
    string UserFullName,
    ReferenceDataChangeAction Action = ReferenceDataChangeAction.Edit,
    byte[]? LastUpdated = null);

/// The kind of change being made to a reference value, as the legacy ChangeActionType.
public enum ReferenceDataChangeAction
{
    /// <summary>Change the lookup value of an existing active value.</summary>
    Edit,

    /// <summary>Add a new value to the table.</summary>
    Add,

    /// <summary>End-date an active value that existing profiles reference.</summary>
    Inactivate,

    /// <summary>Remove an active value that no profile references.</summary>
    Delete
}

/// The placeholder text the legacy screen showed, and wrote to the audit trail, in place of a real value.
public static class ReferenceDataPlaceholders
{
    /// <summary>Shown as the old value when adding a value.</summary>
    public const string NewEntry = "- new entry -";

    /// <summary>Shown as the new value when inactivating a value.</summary>
    public const string ToBeInactivated = "- to be inactivated -";

    /// <summary>Shown as the new value when deleting a value.</summary>
    public const string ToBeDeleted = "- to be deleted -";
}

/// The outcome of a requested reference value change.
public enum ReferenceDataUpdateOutcome
{
    /// <summary>The change was applied and an audit entry written.</summary>
    Updated,

    /// <summary>The value no longer exists in the supplied maintainable table.</summary>
    ValueNotFound,

    /// <summary>Another value in the same table already uses the new lookup value.</summary>
    DuplicateLookupValue,

    /// <summary>The value has been inactivated, so it can no longer be changed.</summary>
    ValueInactive,

    /// <summary>Existing profiles reference the value, so it cannot be deleted.</summary>
    ValueInUse,

    /// <summary>Another user changed the value after it was read, so nothing was written.</summary>
    Conflict
}

/// The result of a requested reference value change.
public sealed record ReferenceDataUpdateResult(
    ReferenceDataUpdateOutcome Outcome,
    ReferenceDataAuditEntry? AuditEntry);
