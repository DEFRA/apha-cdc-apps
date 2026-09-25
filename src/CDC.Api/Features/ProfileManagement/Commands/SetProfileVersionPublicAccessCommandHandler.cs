using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>Handles <see cref="SetProfileVersionPublicAccessCommand"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class SetProfileVersionPublicAccessCommandHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<SetProfileVersionPublicAccessCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The profile version to toggle.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A successful result once the flag has been toggled.</returns>
    public async Task<Result<Unit>> Handle(SetProfileVersionPublicAccessCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await profileManagementService.SetProfileVersionPublicAccessAsync(request.ProfileVersionId, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
