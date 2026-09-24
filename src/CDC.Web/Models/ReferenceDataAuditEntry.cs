namespace CDC.Web.Models;

/// A row of [ReferenceTableAuditLog], as returned by the legacy spgReferenceTableAudit.
public sealed record ReferenceDataAuditEntry(
    Guid Id,
    Guid ReferenceTableId,
    string TableName,
    Guid ReferenceValueId,
    string OldLookupValue,
    string NewLookupValue,
    string UserFullName,
    DateTimeOffset EffectiveDate,
    DateTimeOffset LogDate,
    string Reason);
