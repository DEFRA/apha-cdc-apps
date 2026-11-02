using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>Handles <see cref="GetManageProfileQuery"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
/// <param name="versions">Refuses a version the acting user may not browse.</param>
public sealed class GetManageProfileQueryHandler(IProfileManagementService profileManagementService, IProfileVersionGuard versions)
    : IRequestHandler<GetManageProfileQuery, Result<GetManageProfileResponse>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile's "Manage profile" details, or a not-found result when it does not exist.</returns>
    public async Task<Result<GetManageProfileResponse>> Handle(GetManageProfileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A version link says which version the user means to browse, so a draft they may not read is refused even when the
        // profile has a published version that could be shown instead.
        if (request.ProfileVersionId is { } profileVersionId)
        {
            await versions.EnsureCanBrowseAsync(request.ProfileId, profileVersionId, cancellationToken);
        }

        var response = await profileManagementService.GetManageProfileAsync(request.ProfileId, cancellationToken);

        return response is null
            ? Result.NotFound<GetManageProfileResponse>($"Profile '{request.ProfileId}' was not found.")
            : Result.Success(response);
    }
}
