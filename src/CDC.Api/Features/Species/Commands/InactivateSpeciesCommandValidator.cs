namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Validates <see cref="InactivateSpeciesCommand"/> before it reaches the database.
/// </summary>
public sealed class InactivateSpeciesCommandValidator : AuditedSpeciesChangeCommandValidator<InactivateSpeciesCommand>;
