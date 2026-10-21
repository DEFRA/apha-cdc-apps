using CDC.Api.Domain.Common;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Publishes a profile's current draft version, as the legacy "Publish (public)" and "Publish (Defranet only)" links of the
/// "Manage profile" page did: the draft is validated, and only a valid draft becomes a new published version.
/// </summary>
/// <param name="ProfileId">The profile to publish.</param>
/// <param name="IsPublic">Whether the published version is also made public rather than visible to DefraNet only.</param>
public sealed record PublishProfileCommand(Guid ProfileId, bool IsPublic) : IRequest<Result<PublishProfileResultDto>>;
