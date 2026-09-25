using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileSearch.Queries;

/// <summary>
/// Retrieves every profile summary.
/// </summary>
public sealed record GetAllProfilesQuery : IRequest<Result<IReadOnlyList<ProfileDto>>>;
