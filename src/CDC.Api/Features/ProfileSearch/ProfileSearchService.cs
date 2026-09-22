using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;

namespace CDC.Api.Features.ProfileSearch;

/// <summary>
/// Default implementation of <see cref="IProfileSearchService"/>. This keeps the API layer
/// independent from a concrete repository implementation while preserving the legacy search
/// contract shape expected by the modernised app.
/// </summary>
public sealed class ProfileSearchService : IProfileSearchService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken)
    {
        var profiles = new List<ProfileDto>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Bovine tuberculosis",
                Status = "Published",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Avian influenza",
                Status = "Draft",
                IsActive = true
            }
        };

        return Task.FromResult<IReadOnlyList<ProfileDto>>(profiles);
    }

    /// <inheritdoc />
    public Task<ProfileVersionDto?> GetProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var version = new ProfileVersionDto
        {
            Id = profileVersionId,
            ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            VersionNumber = 1,
            Title = "Legacy profile version",
            Content = "Profile version details",
            IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult<ProfileVersionDto?>(version);
    }
}
