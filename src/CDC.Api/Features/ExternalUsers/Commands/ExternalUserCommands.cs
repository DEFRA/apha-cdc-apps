using CDC.Api.Domain.Common;
using CDC.Api.Features.ExternalUsers.Interfaces;
using MediatR;

namespace CDC.Api.Features.ExternalUsers.Commands;

/// <summary>Subscribes or unsubscribes an external user from review notification emails.</summary>
/// <param name="Id">The user to change.</param>
/// <param name="SubscribedToReviewEmails">Whether the user should receive review notification emails.</param>
public sealed record SetExternalUserSubscriptionCommand(Guid Id, bool SubscribedToReviewEmails) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="SetExternalUserSubscriptionCommand"/>.</summary>
/// <param name="service">External users application service.</param>
public sealed class SetExternalUserSubscriptionCommandHandler(IExternalUsersService service)
    : IRequestHandler<SetExternalUserSubscriptionCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(SetExternalUserSubscriptionCommand request, CancellationToken cancellationToken) =>
        service.SetReviewEmailSubscriptionAsync(request, cancellationToken);
}

/// <summary>Grants an external user a role and section permissions on a profile, or changes an existing grant.</summary>
/// <param name="UserId">The external user.</param>
/// <param name="ProfileId">The profile being granted.</param>
/// <param name="RoleId">The role to grant.</param>
/// <param name="SectionPermissionIds">The profile sections to grant. Must be empty for a reviewer role.</param>
public sealed record SaveExternalUserProfileAccessCommand(
    Guid UserId,
    Guid ProfileId,
    Guid RoleId,
    IReadOnlyList<Guid> SectionPermissionIds) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="SaveExternalUserProfileAccessCommand"/>.</summary>
/// <param name="service">External users application service.</param>
public sealed class SaveExternalUserProfileAccessCommandHandler(IExternalUsersService service)
    : IRequestHandler<SaveExternalUserProfileAccessCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(SaveExternalUserProfileAccessCommand request, CancellationToken cancellationToken) =>
        service.SaveProfileAccessAsync(request, cancellationToken);
}

/// <summary>Removes an external user's access to a profile.</summary>
/// <param name="UserId">The external user.</param>
/// <param name="ProfileId">The profile to remove access to.</param>
public sealed record DeleteExternalUserProfileAccessCommand(Guid UserId, Guid ProfileId) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="DeleteExternalUserProfileAccessCommand"/>.</summary>
/// <param name="service">External users application service.</param>
public sealed class DeleteExternalUserProfileAccessCommandHandler(IExternalUsersService service)
    : IRequestHandler<DeleteExternalUserProfileAccessCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(DeleteExternalUserProfileAccessCommand request, CancellationToken cancellationToken) =>
        service.DeleteProfileAccessAsync(request, cancellationToken);
}
