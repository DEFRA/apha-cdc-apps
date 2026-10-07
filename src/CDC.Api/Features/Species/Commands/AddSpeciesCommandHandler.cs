using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Handles <see cref="AddSpeciesCommand"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class AddSpeciesCommandHandler(
    ISpeciesService speciesService,
    ILogger<AddSpeciesCommandHandler> logger)
    : IRequestHandler<AddSpeciesCommand, Result<AddSpeciesResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The species to add.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new species identifier, or a conflict result when the name is already taken.</returns>
    public async Task<Result<AddSpeciesResultDto>> Handle(
        AddSpeciesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await speciesService.AddSpeciesAsync(request, cancellationToken);

            return Result.Success(result);
        }
        catch (DuplicateSpeciesNameException exception)
        {
            // An expected outcome rather than a fault: the user should pick a different name,
            // so it is reported as a 409 result instead of bubbling up to the middleware.
            logger.DuplicateSpeciesName();

            return Result.Conflict<AddSpeciesResultDto>(exception.Message);
        }
    }
}
