using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Commands;

/// <summary>
/// Clears every recorded answer in one profile version's section that is not read-only, deletes its repeating rows and blanks
/// its revision data, matching the legacy <c>ProfileVersionSection.Clear()</c> + <c>Save()</c> pathway.
/// </summary>
public sealed record ClearProfileVersionSectionCommand : IRequest<Result<ClearProfileSectionResultDto>>
{
    /// <summary>Gets the profile version to clear.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section to clear.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the row version of the section the caller last read (<c>lastUpdated</c> in the answers response). The clear
    /// is refused with a conflict when someone else has saved the section since. When omitted the section is cleared whatever
    /// it holds now.</summary>
    public byte[]? LastUpdated { get; init; }
}

/// <summary>Validates <see cref="ClearProfileVersionSectionCommand"/>.</summary>
public sealed class ClearProfileVersionSectionCommandValidator : AbstractValidator<ClearProfileVersionSectionCommand>
{
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="ClearProfileVersionSectionCommandValidator"/> class.</summary>
    public ClearProfileVersionSectionCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(command => command.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");

        RuleFor(command => command.LastUpdated)
            .Must(value => value is null or { Length: RowVersionLength })
            .WithMessage("The section's row version is not valid.");
    }
}

/// <summary>
/// Handles <see cref="ClearProfileVersionSectionCommand"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ClearProfileVersionSectionCommandHandler(
    IProfileSectionService profileSectionService,
    ILogger<ClearProfileVersionSectionCommandHandler> logger)
    : IRequestHandler<ClearProfileVersionSectionCommand, Result<ClearProfileSectionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of values cleared, or a conflict result when the section was saved by another user or the
    /// profile version is not a current draft.</returns>
    public async Task<Result<ClearProfileSectionResultDto>> Handle(
        ClearProfileVersionSectionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await profileSectionService.ClearProfileVersionSectionAsync(
                request.ProfileVersionId,
                request.ProfileSectionId,
                request.LastUpdated,
                cancellationToken);

            return Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            logger.ProfileSectionConcurrencyConflict(request.ProfileVersionId, request.ProfileSectionId);

            return Result.Conflict<ClearProfileSectionResultDto>(exception.Message);
        }
    }
}
