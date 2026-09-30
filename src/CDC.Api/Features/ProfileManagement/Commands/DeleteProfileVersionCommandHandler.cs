using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="DeleteProfileVersionCommand"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class DeleteProfileVersionCommandHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<DeleteProfileVersionCommand, Result<DeleteProfileVersionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The profile version to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Which version is now latest, or a not-found result.</returns>
    public async Task<Result<DeleteProfileVersionResultDto>> Handle(
        DeleteProfileVersionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileManagementService.DeleteProfileVersionAsync(request.ProfileVersionId, cancellationToken);

        return result is null
            ? Result.NotFound<DeleteProfileVersionResultDto>(
                $"Profile version '{request.ProfileVersionId}' was not found. Another user may have altered the profile.")
            : Result.Success(result);
    }
}
