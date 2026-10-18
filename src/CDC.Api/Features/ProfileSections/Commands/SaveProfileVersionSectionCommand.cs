using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Commands;

/// <summary>
/// Saves the answers submitted for one profile version's section, matching the legacy save of the "Edit profile questions"
/// page for standard (non-per-species, non-repeating) questions: only the answers that changed are written, after the
/// questions the answers have made irrelevant have been cleared.
/// </summary>
public sealed record SaveProfileVersionSectionCommand : IRequest<Result<SaveProfileSectionResultDto>>
{
    /// <summary>Gets the profile version being answered.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section being answered.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the submitted field answers.</summary>
    public required IReadOnlyList<ProfileFieldAnswerInputDto> Answers { get; init; }

    /// <summary>Gets the row version of the section the caller last read (<c>lastUpdated</c> in the answers response). The save
    /// is refused with a conflict when someone else has saved the section since. When omitted the save is made over whatever
    /// the section holds now.</summary>
    public byte[]? LastUpdated { get; init; }
}

/// <summary>Validates <see cref="SaveProfileVersionSectionCommand"/>.</summary>
public sealed class SaveProfileVersionSectionCommandValidator : AbstractValidator<SaveProfileVersionSectionCommand>
{
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="SaveProfileVersionSectionCommandValidator"/> class.</summary>
    public SaveProfileVersionSectionCommandValidator()
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
/// Handles <see cref="SaveProfileVersionSectionCommand"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SaveProfileVersionSectionCommandHandler(
    IProfileSectionService profileSectionService,
    ILogger<SaveProfileVersionSectionCommandHandler> logger)
    : IRequestHandler<SaveProfileVersionSectionCommand, Result<SaveProfileSectionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of values saved, or a conflict result when the section was saved by another user or the
    /// profile version is not a current draft.</returns>
    public async Task<Result<SaveProfileSectionResultDto>> Handle(
        SaveProfileVersionSectionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await profileSectionService.SaveProfileVersionSectionAsync(
                request.ProfileVersionId,
                request.ProfileSectionId,
                request.Answers,
                request.LastUpdated,
                cancellationToken);

            return Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            logger.ProfileSectionConcurrencyConflict(request.ProfileVersionId, request.ProfileSectionId);

            return Result.Conflict<SaveProfileSectionResultDto>(exception.Message);
        }
    }
}
