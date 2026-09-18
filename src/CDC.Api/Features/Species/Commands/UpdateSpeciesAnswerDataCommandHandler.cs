using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Commands;

/// <summary>
/// Handles <see cref="UpdateSpeciesAnswerDataCommand"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateSpeciesAnswerDataCommandHandler(
    ISpeciesService speciesService,
    ILogger<UpdateSpeciesAnswerDataCommandHandler> logger)
    : IRequestHandler<UpdateSpeciesAnswerDataCommand, Result<UpdateSpeciesAnswerDataResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new row version, or a conflict result when another user has saved first.</returns>
    public async Task<Result<UpdateSpeciesAnswerDataResultDto>> Handle(
        UpdateSpeciesAnswerDataCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await speciesService.UpdateSpeciesAnswerDataAsync(request, cancellationToken);

            return Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            // An expected outcome rather than a fault: the client should re-read and retry,
            // so it is reported as a 409 result instead of bubbling up to the middleware.
            logger.ConcurrencyConflict(request.SpeciesId);

            return Result.Conflict<UpdateSpeciesAnswerDataResultDto>(exception.Message);
        }
    }
}
