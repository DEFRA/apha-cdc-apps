namespace CDC.Api.Features.ReferenceData.Dtos;

/// <summary>One recorded change to a maintainable reference table, as <c>spgReferenceTableAudit</c> returns it.</summary>
public sealed record ReferenceTableAuditEntryDto
{
    /// <summary>Gets the audit entry identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the reference table changed.</summary>
    public Guid ReferenceTableId { get; init; }

    /// <summary>Gets the name of the reference table changed.</summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>Gets the value before the change, or <c>- new entry -</c> for an addition.</summary>
    public string OldLookupValue { get; init; } = string.Empty;

    /// <summary>Gets the value after the change, or <c>- to be inactivated -</c>/<c>- to be deleted -</c>.</summary>
    public string NewLookupValue { get; init; } = string.Empty;

    /// <summary>Gets the full name of the user who made the change.</summary>
    public string UserFullName { get; init; } = string.Empty;

    /// <summary>Gets the date the change took effect.</summary>
    public DateTime EffectiveDate { get; init; }

    /// <summary>Gets the date the change was made.</summary>
    public DateTime LogDate { get; init; }

    /// <summary>Gets the reason given for the change, which may carry links.</summary>
    public string Reason { get; init; } = string.Empty;
}
