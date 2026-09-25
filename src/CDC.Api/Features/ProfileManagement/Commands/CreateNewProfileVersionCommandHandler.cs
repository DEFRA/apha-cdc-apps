using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="CreateNewProfileVersionCommand"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class CreateNewProfileVersionCommandHandler(
    IProfileManagementService profileManagementService,
    ILogger<CreateNewProfileVersionCommandHandler> logger)
    : IRequestHandler<CreateNewProfileVersionCommand, Result<NewProfileVersionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The profile version to base the new version on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The new profile version's identifier, or a conflict result when the source version is
    /// not the latest, the publish/public rules are violated, or it has no active species.
    /// </returns>
    public async Task<Result<NewProfileVersionResultDto>> Handle(
        CreateNewProfileVersionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await profileManagementService.CreateNewProfileVersionAsync(request, cancellationToken);

            return Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            logger.NewProfileVersionRejected(request.ProfileVersionId, exception.Message);

            return Result.Conflict<NewProfileVersionResultDto>(exception.Message);
        }
    }
}
