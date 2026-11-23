using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="CreateProfileCommand"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class CreateProfileCommandHandler(
    IProfileManagementService profileManagementService,
    ILogger<CreateProfileCommandHandler> logger)
    : IRequestHandler<CreateProfileCommand, Result<CreateProfileResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The profile to create.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new profile's identifier and row version, or a validation failure when the title is refused (for example already in use).</returns>
    public async Task<Result<CreateProfileResultDto>> Handle(CreateProfileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await profileManagementService.CreateProfileAsync(request, cancellationToken);

            return Result.Success(result);
        }
        catch (ProfileTitleRefusedException exception)
        {
            logger.ProfileTitleRefused(request.Id);

            return Result.ValidationFailed<CreateProfileResultDto>(exception.Message);
        }
    }
}
