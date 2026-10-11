using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Commands;

/// <summary>
/// Saves every submitted field answer in one profile version's section, matching the legacy
/// <c>IProfileField.Save()</c> pathway for standard (non-per-species, non-repeating) questions.
/// </summary>
public sealed record SaveProfileVersionSectionCommand : IRequest<Result<SaveProfileSectionResultDto>>
{
    /// <summary>Gets the profile version being answered.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section being answered.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the submitted field answers.</summary>
    public required IReadOnlyList<ProfileFieldAnswerInputDto> Answers { get; init; }
}

/// <summary>Validates <see cref="SaveProfileVersionSectionCommand"/>.</summary>
public sealed class SaveProfileVersionSectionCommandValidator : AbstractValidator<SaveProfileVersionSectionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="SaveProfileVersionSectionCommandValidator"/> class.</summary>
    public SaveProfileVersionSectionCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(command => command.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>
/// Handles <see cref="SaveProfileVersionSectionCommand"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class SaveProfileVersionSectionCommandHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<SaveProfileVersionSectionCommand, Result<SaveProfileSectionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of fields saved.</returns>
    public async Task<Result<SaveProfileSectionResultDto>> Handle(
        SaveProfileVersionSectionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileSectionService.SaveProfileVersionSectionAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            request.Answers,
            cancellationToken);

        return Result.Success(result);
    }
}
