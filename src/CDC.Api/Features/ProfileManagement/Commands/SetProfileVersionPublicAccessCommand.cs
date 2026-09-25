using CDC.Api.Domain.Common;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Toggles a profile version's public visibility flag. Mirrors the legacy
/// <c>SetProfileVersionPublicAccessRequest</c> data contract, which carries no explicit
/// on/off flag: <c>spuProfileVersionPublicFlag</c> flips the current value.
/// </summary>
/// <param name="ProfileVersionId">The profile version to toggle.</param>
public sealed record SetProfileVersionPublicAccessCommand(Guid ProfileVersionId) : IRequest<Result<Unit>>;
