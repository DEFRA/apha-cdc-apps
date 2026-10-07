namespace CDC.Web.Models;

/// A maintainable reference table, mirroring [ReferenceTable] where [IsMaintainable] = 1.
public sealed record ReferenceTableSummary(Guid Id, string Name);
