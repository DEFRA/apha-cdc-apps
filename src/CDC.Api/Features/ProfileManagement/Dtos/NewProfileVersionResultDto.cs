namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>Outcome of creating a new profile version. Returned by <c>POST /api/profiles/versions</c>.</summary>
public sealed record NewProfileVersionResultDto
{
    /// <summary>Gets the newly created profile version's identifier.</summary>
    public Guid NewProfileVersionId { get; init; }
}
