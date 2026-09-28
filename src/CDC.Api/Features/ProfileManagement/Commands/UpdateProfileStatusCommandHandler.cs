using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="UpdateProfileStatusCommand"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class UpdateProfileStatusCommandHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<UpdateProfileStatusCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The status change to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// A successful result once the status has been set, or a not-found result when
    /// <see cref="UpdateProfileStatusCommand.ProfileStatusId"/> does not exist.
    /// </returns>
    public async Task<Result<Unit>> Handle(UpdateProfileStatusCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statusTypes = await profileManagementService.GetProfileStatusTypesAsync(cancellationToken);

        if (!statusTypes.Any(status => status.Id == request.ProfileStatusId))
        {
            return Result.NotFound<Unit>($"Profile status '{request.ProfileStatusId}' was not found.");
        }

        await profileManagementService.UpdateProfileStatusAsync(request.ProfileId, request.ProfileStatusId, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
