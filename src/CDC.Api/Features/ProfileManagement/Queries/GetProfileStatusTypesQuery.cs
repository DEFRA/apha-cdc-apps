using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>Retrieves every profile status a profile can be set to.</summary>
public sealed record GetProfileStatusTypesQuery : IRequest<Result<IReadOnlyList<ProfileStatusTypeDto>>>;

/// <summary>Handles <see cref="GetProfileStatusTypesQuery"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class GetProfileStatusTypesQueryHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<GetProfileStatusTypesQuery, Result<IReadOnlyList<ProfileStatusTypeDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every profile status.</returns>
    public async Task<Result<IReadOnlyList<ProfileStatusTypeDto>>> Handle(
        GetProfileStatusTypesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statusTypes = await profileManagementService.GetProfileStatusTypesAsync(cancellationToken);

        return Result.Success(statusTypes);
    }
}
