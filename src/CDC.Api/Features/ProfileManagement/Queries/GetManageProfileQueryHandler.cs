using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>Handles <see cref="GetManageProfileQuery"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class GetManageProfileQueryHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<GetManageProfileQuery, Result<GetManageProfileResponse>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile's "Manage profile" details, or a not-found result when it does not exist.</returns>
    public async Task<Result<GetManageProfileResponse>> Handle(GetManageProfileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await profileManagementService.GetManageProfileAsync(request.ProfileId, cancellationToken);

        return response is null
            ? Result.NotFound<GetManageProfileResponse>($"Profile '{request.ProfileId}' was not found.")
            : Result.Success(response);
    }
}
