namespace CDC.Web.Models;

/// A requested change to a reference value, carrying the audit metadata spuReferenceValue needs.
/// The effective date is set by the system at the point of change, as it is in the legacy procedure.
public sealed record ReferenceDataUpdate(
    Guid ReferenceTableId,
    Guid ValueId,
    string NewLookupValue,
    string Reason,
    string UserFullName);

/// The outcome of a requested reference value change.
public enum ReferenceDataUpdateOutcome
{
    /// <summary>The value was changed and an audit entry written.</summary>
    Updated,

    /// <summary>The value no longer exists in the supplied maintainable table.</summary>
    ValueNotFound,

    /// <summary>Another value in the same table already uses the new lookup value.</summary>
    DuplicateLookupValue
}

/// The result of a requested reference value change.
public sealed record ReferenceDataUpdateResult(
    ReferenceDataUpdateOutcome Outcome,
    ReferenceDataAuditEntry? AuditEntry);
