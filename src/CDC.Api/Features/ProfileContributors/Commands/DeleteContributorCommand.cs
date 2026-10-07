using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Commands;

/// <summary>
/// Removes a contributor from a profile. Mirrors the legacy
/// <c>grdProfileContributors_RowCommand</c> "DeleteContributor" handler.
/// </summary>
/// <param name="ProfileId">The profile to remove the contributor from.</param>
/// <param name="ContributorId">The contributor (user) being removed.</param>
/// <param name="LastUpdated">The row version last read for this contributor.</param>
public sealed record DeleteContributorCommand(Guid ProfileId, Guid ContributorId, byte[] LastUpdated) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="DeleteContributorCommand"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class DeleteContributorCommandHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<DeleteContributorCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(DeleteContributorCommand request, CancellationToken cancellationToken) =>
        profileContributorsService.DeleteContributorAsync(request, cancellationToken);
}
