using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Queries;

/// <summary>
/// Retrieves the full editable detail for one profile contributor.
/// </summary>
public sealed record GetContributorForEditQuery(Guid ProfileId, Guid ContributorId) : IRequest<Result<ContributorEditDto>>;

/// <summary>Handles <see cref="GetContributorForEditQuery"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class GetContributorForEditQueryHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<GetContributorForEditQuery, Result<ContributorEditDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The contributor's full editable detail, or a not-found result.</returns>
    public async Task<Result<ContributorEditDto>> Handle(GetContributorForEditQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contributor = await profileContributorsService.GetContributorForEditAsync(
            request.ProfileId, request.ContributorId, cancellationToken);

        return contributor is null
            ? Result.NotFound<ContributorEditDto>(
                $"Contributor '{request.ContributorId}' was not found on profile '{request.ProfileId}'.")
            : Result.Success(contributor);
    }
}
