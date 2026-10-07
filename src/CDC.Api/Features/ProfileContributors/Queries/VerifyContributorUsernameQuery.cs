using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Queries;

/// <summary>
/// Verifies a username for the "Add profile contributor" lookup step. Mirrors the legacy
/// <c>VerifyDatabaseUserCommand</c>/<c>btnLookup_Click</c>.
/// </summary>
/// <param name="UserName">The username to verify.</param>
public sealed record VerifyContributorUsernameQuery(string UserName) : IRequest<Result<UserVerificationResultDto>>;

/// <summary>Handles <see cref="VerifyContributorUsernameQuery"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class VerifyContributorUsernameQueryHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<VerifyContributorUsernameQuery, Result<UserVerificationResultDto>>
{
    /// <inheritdoc />
    public async Task<Result<UserVerificationResultDto>> Handle(VerifyContributorUsernameQuery request, CancellationToken cancellationToken)
    {
        var result = await profileContributorsService.VerifyUsernameAsync(request.UserName, cancellationToken);

        return Result.Success(result);
    }
}
