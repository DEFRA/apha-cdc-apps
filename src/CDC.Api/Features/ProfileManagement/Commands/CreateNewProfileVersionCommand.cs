using System.Text.Json.Serialization;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Commands;

/// <summary>
/// Creates a new version of a profile, cloning its affected species and (when publishing)
/// recalculating prioritisation scores. Mirrors the legacy <c>NewProfileVersionRequest</c>
/// data contract and replicates <c>NewProfileVersionCommand.vb</c> exactly.
/// </summary>
/// <param name="ProfileVersionId">The profile version to base the new version on. Must be the latest version.</param>
/// <param name="IsPublished">
/// Whether the new version is published (bumps the major version number) rather than a draft
/// (bumps the minor version number).
/// </param>
/// <param name="IsPublic">
/// Whether the new version is publicly visible. Only valid when <paramref name="IsPublished"/>
/// is <see langword="true"/>.
/// </param>
public sealed record CreateNewProfileVersionCommand(
    [property: JsonRequired] Guid ProfileVersionId,
    [property: JsonRequired] bool IsPublished,
    [property: JsonRequired] bool IsPublic)
    : IRequest<Result<NewProfileVersionResultDto>>;
