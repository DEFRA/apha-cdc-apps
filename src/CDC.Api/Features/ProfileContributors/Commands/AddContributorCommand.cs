using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Commands;

/// <summary>
/// Adds a new profile contributor: an existing global user not yet on this profile, or a
/// brand-new username not yet known to the system. Mirrors the legacy
/// <c>btnLookup_Click</c>/<c>btnSave_Click</c> "Add" flow.
/// </summary>
/// <param name="ProfileId">The profile to add the contributor to.</param>
/// <param name="ContributorId">The user id: the looked-up existing global user's id, or a freshly
/// generated id for a brand-new username.</param>
/// <param name="UserName">The contributor's username. Only used when <paramref name="ContributorId"/>
/// does not yet exist as a global user.</param>
/// <param name="IsSsoUser">Whether the looked-up global user is an SSO user. Always <see langword="false"/> for a brand-new username.</param>
/// <param name="RoleId">The contributor's role.</param>
/// <param name="FullName">The user's full name. Ignored for an SSO user.</param>
/// <param name="Organisation">The user's organisation. Ignored for an SSO user.</param>
/// <param name="SectionPermissionIds">The profile sections the contributor may edit. Must be
/// empty unless <paramref name="RoleId"/> is a contributor role.</param>
public sealed record AddContributorCommand(
    Guid ProfileId,
    Guid ContributorId,
    string UserName,
    bool IsSsoUser,
    Guid RoleId,
    string FullName,
    string Organisation,
    IReadOnlyList<Guid> SectionPermissionIds) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="AddContributorCommand"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class AddContributorCommandHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<AddContributorCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(AddContributorCommand request, CancellationToken cancellationToken) =>
        profileContributorsService.AddContributorAsync(request, cancellationToken);
}
