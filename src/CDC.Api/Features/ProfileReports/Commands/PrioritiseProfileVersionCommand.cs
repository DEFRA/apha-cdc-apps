using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Commands;

/// <summary>
/// Recalculates a profile version's prioritisation scores. The legacy draft summary prioritisation report did this
/// (<c>PrioritiseProfileCommand.PrioritiseProfile</c>) before it built the report, so the report never printed stale scores.
/// </summary>
/// <param name="ProfileVersionId">The profile version to prioritise.</param>
public sealed record PrioritiseProfileVersionCommand(Guid ProfileVersionId) : IRequest<Result<Unit>>;

/// <summary>Validates <see cref="PrioritiseProfileVersionCommand"/>.</summary>
public sealed class PrioritiseProfileVersionCommandValidator : AbstractValidator<PrioritiseProfileVersionCommand>
{
    /// <summary>Initialises a new instance of the <see cref="PrioritiseProfileVersionCommandValidator"/> class.</summary>
    public PrioritiseProfileVersionCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="PrioritiseProfileVersionCommand"/>.</summary>
/// <param name="contentService">Prioritises the profile version.</param>
public sealed class PrioritiseProfileVersionCommandHandler(IProfileReportContentService contentService)
    : IRequestHandler<PrioritiseProfileVersionCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A successful result once the scores have been rebuilt.</returns>
    public Task<Result<Unit>> Handle(PrioritiseProfileVersionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return contentService.PrioritiseProfileVersionAsync(request.ProfileVersionId, cancellationToken);
    }
}
