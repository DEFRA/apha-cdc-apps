namespace CDC.Web.Models;

/// A row of [ReferenceTableAuditLog], as returned by the legacy spgReferenceTableAudit.
public sealed record ReferenceDataAuditEntry(
    Guid Id,
    Guid ReferenceTableId,
    string TableName,
    string OldLookupValue,
    string NewLookupValue,
    string UserFullName,
    DateTime EffectiveDate,
    DateTime LogDate,
    string Reason);
