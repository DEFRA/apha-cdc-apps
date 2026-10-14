namespace CDC.Api.Features.Species;

/// <summary>
/// Source-generated structured log messages for the species feature. Nothing here records
/// personal data: only identifiers, counts and the disease filter term are logged.
/// </summary>
internal static partial class SpeciesLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Retrieved {SpeciesCount} species")]
    public static partial void RetrievedAllSpecies(this ILogger logger, int speciesCount);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Retrieved {SpeciesCount} species selected for disease {DiseaseName}")]
    public static partial void RetrievedSelectedSpecies(this ILogger logger, int speciesCount, string diseaseName);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Retrieved species metadata containing {SectionCount} sections")]
    public static partial void RetrievedSpeciesMetadata(this ILogger logger, int sectionCount);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Retrieved answer data for species {SpeciesId} containing {SectionCount} sections")]
    public static partial void RetrievedSpeciesAnswerData(this ILogger logger, Guid speciesId, int sectionCount);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "No answer data found for species {SpeciesId}")]
    public static partial void SpeciesAnswerDataNotFound(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Applying {ChangeCount} answer changes to species {SpeciesId}")]
    public static partial void UpdatingSpeciesAnswerData(this ILogger logger, int changeCount, Guid speciesId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Committed answer changes for species {SpeciesId}")]
    public static partial void UpdatedSpeciesAnswerData(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Rejected a concurrent edit to species {SpeciesId}")]
    public static partial void ConcurrencyConflict(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Error, Message = "Rolled back the answer data transaction for species {SpeciesId}")]
    public static partial void UpdateRolledBack(this ILogger logger, Exception exception, Guid speciesId);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Retrieved detail for species {SpeciesId}")]
    public static partial void RetrievedSpeciesDetail(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "No species found with id {SpeciesId}")]
    public static partial void SpeciesDetailNotFound(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "Retrieved {ValidParentCount} valid parents for species {SpeciesId}")]
    public static partial void RetrievedSpeciesValidParents(this ILogger logger, int validParentCount, Guid speciesId);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Information, Message = "Updating name/parent for species {SpeciesId}")]
    public static partial void UpdatingSpeciesNameParent(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Information, Message = "Updated name/parent for species {SpeciesId}")]
    public static partial void UpdatedSpeciesNameParent(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "Retrieved {EntryCount} species audit trail entries")]
    public static partial void RetrievedSpeciesAuditTrail(this ILogger logger, int entryCount);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "Adding a new species to the hierarchy")]
    public static partial void AddingSpecies(this ILogger logger);

    [LoggerMessage(EventId = 1017, Level = LogLevel.Information, Message = "Added species {SpeciesId} to the hierarchy")]
    public static partial void AddedSpecies(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1018, Level = LogLevel.Warning, Message = "Rejected a species name that is already in use")]
    public static partial void DuplicateSpeciesName(this ILogger logger);

    [LoggerMessage(EventId = 1019, Level = LogLevel.Information, Message = "Moved species {SpeciesId} {Direction}")]
    private static partial void ChangedSpeciesPositionCore(this ILogger logger, Guid speciesId, string direction);

    public static void ChangedSpeciesPosition(this ILogger logger, Guid speciesId, bool isMovingUp) =>
        logger.ChangedSpeciesPositionCore(speciesId, isMovingUp ? "up" : "down");

    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "Inactivating species {SpeciesId}")]
    public static partial void InactivatingSpecies(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Information, Message = "Inactivated species {SpeciesId}")]
    public static partial void InactivatedSpecies(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1022, Level = LogLevel.Information, Message = "Deleting species {SpeciesId}")]
    public static partial void DeletingSpecies(this ILogger logger, Guid speciesId);

    [LoggerMessage(EventId = 1023, Level = LogLevel.Information, Message = "Deleted species {SpeciesId}")]
    public static partial void DeletedSpecies(this ILogger logger, Guid speciesId);
}
