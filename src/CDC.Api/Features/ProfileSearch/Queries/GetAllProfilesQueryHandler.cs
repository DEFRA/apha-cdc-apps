using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileSearch.Queries;

/// <summary>
/// Handles <see cref="GetAllProfilesQuery"/>.
/// </summary>
public sealed class GetAllProfilesQueryHandler(IProfileSearchService profileSearchService)
    : IRequestHandler<GetAllProfilesQuery, Result<IReadOnlyList<ProfileDto>>>
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ProfileDto>>> Handle(
        GetAllProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var profiles = await profileSearchService.GetAllProfilesAsync(cancellationToken);

        return Result.Success(profiles);
    }
}
