using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Queries;

/// <summary>
/// Retrieves one page of a profile's contributors.
/// </summary>
/// <param name="ProfileId">The profile to read.</param>
/// <param name="PageNumber">The 1-based page to return.</param>
/// <param name="PageSize">The number of items per page, or 0/negative for every contributor on a single page.</param>
public sealed record GetProfileContributorsQuery(Guid ProfileId, int PageNumber, int PageSize)
    : IRequest<Result<PagedResult<ContributorDto>>>;

/// <summary>Handles <see cref="GetProfileContributorsQuery"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class GetProfileContributorsQueryHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<GetProfileContributorsQuery, Result<PagedResult<ContributorDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The requested page of contributors.</returns>
    public async Task<Result<PagedResult<ContributorDto>>> Handle(GetProfileContributorsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileContributorsService.GetProfileContributorsAsync(
            request.ProfileId, request.PageNumber, request.PageSize, cancellationToken);

        return Result.Success(result);
    }
}
