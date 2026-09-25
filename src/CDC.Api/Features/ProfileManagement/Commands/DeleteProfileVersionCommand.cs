using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Deletes a profile version and its affected species. Mirrors the legacy
/// <c>DeleteProfileVersionRequest</c> data contract.
/// </summary>
/// <param name="ProfileVersionId">The profile version to delete.</param>
public sealed record DeleteProfileVersionCommand(Guid ProfileVersionId) : IRequest<Result<DeleteProfileVersionResultDto>>;
