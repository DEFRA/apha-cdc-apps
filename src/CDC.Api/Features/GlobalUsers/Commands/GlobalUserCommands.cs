using CDC.Api.Domain.Common;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.GlobalUsers.Commands;

/// <summary>
/// Adds or edits a global user. Mirrors the legacy <c>btnSave_Click</c> handler.
/// </summary>
/// <param name="Id">The user's identifier.</param>
/// <param name="IsNew">Whether this is the "Add" flow; for an edit the stored username is kept.</param>
/// <param name="UserName">The username. Ignored when the user already exists.</param>
/// <param name="FullName">The user's full name.</param>
/// <param name="Organisation">The user's organisation.</param>
/// <param name="Role">The system role to assign.</param>
/// <param name="SubscribedToReviewEmails">Whether the user receives review notification emails.</param>
/// <param name="LastUpdated">The row version last read for this user.</param>
public sealed record SaveGlobalUserCommand(
    Guid Id,
    bool IsNew,
    string UserName,
    string FullName,
    string Organisation,
    GlobalUserRole Role,
    bool SubscribedToReviewEmails,
    byte[] LastUpdated) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="SaveGlobalUserCommand"/>.</summary>
/// <param name="service">Global users application service.</param>
public sealed class SaveGlobalUserCommandHandler(IGlobalUsersService service)
    : IRequestHandler<SaveGlobalUserCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(SaveGlobalUserCommand request, CancellationToken cancellationToken) =>
        service.SaveAsync(request, cancellationToken);
}

/// <summary>Revokes a user's global role. Mirrors the legacy "DeleteGlobalUser" row command.</summary>
/// <param name="Id">The user whose role is revoked.</param>
/// <param name="LastUpdated">The row version last read for this user.</param>
public sealed record DeleteGlobalUserCommand(Guid Id, byte[] LastUpdated) : IRequest<Result<Unit>>;

/// <summary>Handles <see cref="DeleteGlobalUserCommand"/>.</summary>
/// <param name="service">Global users application service.</param>
public sealed class DeleteGlobalUserCommandHandler(IGlobalUsersService service)
    : IRequestHandler<DeleteGlobalUserCommand, Result<Unit>>
{
    /// <inheritdoc />
    public Task<Result<Unit>> Handle(DeleteGlobalUserCommand request, CancellationToken cancellationToken) =>
        service.DeleteAsync(request, cancellationToken);
}
