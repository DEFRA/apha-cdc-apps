using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="PublishProfileCommand"/>.</summary>
/// <param name="publishingService">Profile publishing application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class PublishProfileCommandHandler(
    IProfilePublishingService publishingService,
    ILogger<PublishProfileCommandHandler> logger)
    : IRequestHandler<PublishProfileCommand, Result<PublishProfileResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The profile to publish.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The new published version, or the validation errors that stopped it being published; or a conflict result when the
    /// draft is not the latest version of the profile or it has no active profiled species.
    /// </returns>
    public async Task<Result<PublishProfileResultDto>> Handle(PublishProfileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await publishingService.PublishAsync(request.ProfileId, request.IsPublic, cancellationToken);
        }
        catch (ConcurrencyException exception)
        {
            logger.PublishRefused(request.ProfileId, exception.Message);

            return Result.Conflict<PublishProfileResultDto>(exception.Message);
        }
    }
}
