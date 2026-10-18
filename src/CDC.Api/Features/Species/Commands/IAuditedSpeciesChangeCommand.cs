namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Common shape shared by commands that change a species and record an audit trail entry with a
/// mandatory reason and an optimistic-concurrency row version (for example delete and
/// inactivate). Lets their validators share one set of rules.
/// </summary>
public interface IAuditedSpeciesChangeCommand
{
    /// <summary>Gets the species being changed.</summary>
    Guid SpeciesId { get; }

    /// <summary>Gets the reason given for the change. Recorded in the audit trail.</summary>
    string Reason { get; }

    /// <summary>Gets the id of the <c>[User]</c> row recorded as the author of the change.</summary>
    Guid UserId { get; }

    /// <summary>Gets the row version last read for this species.</summary>
    byte[] LastUpdated { get; }
}
