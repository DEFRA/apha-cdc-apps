using CDC.Api.Domain.Common;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Updates a profile's status. Mirrors the legacy <c>UpdateProfileStatusRequest</c> data
/// contract. The legacy <c>UpdateProfileStatus</c> operation applies no transition rules of
/// its own beyond the status existing, so none are invented here either.
/// </summary>
/// <param name="ProfileId">The profile to update.</param>
/// <param name="ProfileStatusId">The status to set.</param>
public sealed record UpdateProfileStatusCommand(Guid ProfileId, Guid ProfileStatusId) : IRequest<Result<Unit>>;
