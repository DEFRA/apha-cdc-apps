using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Commands;

/// <summary>
/// Changes a contributor's role, (for non-SSO users) full name and organisation, and the set of
/// profile sections they may edit. Mirrors the legacy <c>btnSave_Click</c> handler.
/// </summary>
/// <param name="ProfileId">The profile the contributor belongs to.</param>
/// <param name="ContributorId">The contributor (user) being updated.</param>
/// <param name="RoleId">The contributor's new role.</param>
/// <param name="FullName">The user's full name. Ignored for an SSO user.</param>
/// <param name="Organisation">The user's organisation. Ignored for an SSO user.</param>
/// <param name="SectionPermissionIds">The profile sections the contributor may edit. Must be
/// empty unless <paramref name="RoleId"/> is a contributor role.</param>
/// <param name="LastUpdated">The row version last read for this contributor.</param>
public sealed record UpdateContributorCommand(
    Guid ProfileId,
    Guid ContributorId,
    Guid RoleId,
    string FullName,
    string Organisation,
    IReadOnlyList<Guid> SectionPermissionIds,
    byte[] LastUpdated) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="UpdateContributorCommand"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class UpdateContributorCommandHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<UpdateContributorCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the business validation/concurrency/not-found failure.</returns>
    public async Task<Result<Unit>> Handle(UpdateContributorCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await profileContributorsService.UpdateContributorAsync(request, cancellationToken);
    }
}
