using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileSearch.Queries;

/// <summary>
/// Handles <see cref="GetProfileVersionQuery"/>.
/// </summary>
public sealed class GetProfileVersionQueryHandler(IProfileSearchService profileSearchService)
    : IRequestHandler<GetProfileVersionQuery, Result<ProfileVersionDto>>
{
    /// <inheritdoc />
    public async Task<Result<ProfileVersionDto>> Handle(
        GetProfileVersionQuery request,
        CancellationToken cancellationToken)
    {
        var profileVersion = await profileSearchService.GetProfileVersionAsync(request.ProfileVersionId, cancellationToken);

        return profileVersion is null
            ? Result.NotFound<ProfileVersionDto>($"Profile version '{request.ProfileVersionId}' was not found.")
            : Result.Success(profileVersion);
    }
}
