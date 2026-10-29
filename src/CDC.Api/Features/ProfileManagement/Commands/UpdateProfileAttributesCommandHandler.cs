using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="UpdateProfileAttributesCommand"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateProfileAttributesCommandHandler(
    IProfileManagementService profileManagementService,
    ILogger<UpdateProfileAttributesCommandHandler> logger)
    : IRequestHandler<UpdateProfileAttributesCommand, Result<UpdateProfileAttributesResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new row version, a forbidden result when the user may not edit a profile, a conflict result when another user has saved first, or a validation failure when the title is refused.</returns>
    public async Task<Result<UpdateProfileAttributesResultDto>> Handle(
        UpdateProfileAttributesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await profileManagementService.UpdateProfileAttributesAsync(request, cancellationToken);
        }
        catch (ConcurrencyException exception)
        {
            logger.ConcurrencyConflict(request.Id);

            return Result.Conflict<UpdateProfileAttributesResultDto>(exception.Message);
        }
        catch (ProfileTitleRefusedException exception)
        {
            logger.ProfileTitleRefused(request.Id);

            return Result.ValidationFailed<UpdateProfileAttributesResultDto>(exception.Message);
        }
    }
}
