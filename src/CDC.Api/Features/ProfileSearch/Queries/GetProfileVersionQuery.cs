using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileSearch.Queries;

/// <summary>
/// Retrieves a single profile version.
/// </summary>
public sealed record GetProfileVersionQuery(Guid ProfileVersionId) : IRequest<Result<ProfileVersionDto>>;
