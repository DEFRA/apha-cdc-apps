using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Commands;

/// <summary>
/// Clears every recorded answer in one profile version's section, matching the legacy
/// <c>ProfileVersionSection.Clear()</c> + <c>Save()</c> pathway.
/// </summary>
/// <param name="ProfileVersionId">The profile version to clear.</param>
/// <param name="ProfileSectionId">The section to clear.</param>
public sealed record ClearProfileVersionSectionCommand(Guid ProfileVersionId, Guid ProfileSectionId)
    : IRequest<Result<ClearProfileSectionResultDto>>;

/// <summary>Validates <see cref="ClearProfileVersionSectionCommand"/>.</summary>
public sealed class ClearProfileVersionSectionCommandValidator : AbstractValidator<ClearProfileVersionSectionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="ClearProfileVersionSectionCommandValidator"/> class.</summary>
    public ClearProfileVersionSectionCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(command => command.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>
/// Handles <see cref="ClearProfileVersionSectionCommand"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class ClearProfileVersionSectionCommandHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<ClearProfileVersionSectionCommand, Result<ClearProfileSectionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of fields cleared.</returns>
    public async Task<Result<ClearProfileSectionResultDto>> Handle(
        ClearProfileVersionSectionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileSectionService.ClearProfileVersionSectionAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            cancellationToken);

        return Result.Success(result);
    }
}
