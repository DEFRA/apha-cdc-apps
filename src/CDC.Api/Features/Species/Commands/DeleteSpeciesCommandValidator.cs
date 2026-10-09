namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Validates <see cref="DeleteSpeciesCommand"/> before it reaches the database.
/// </summary>
public sealed class DeleteSpeciesCommandValidator : AuditedSpeciesChangeCommandValidator<DeleteSpeciesCommand>;
