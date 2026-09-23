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
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Brucellosis",
                Status = "Published",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Bluetongue",
                Status = "Draft",
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "Foot and mouth disease",
                Status = "Published",
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
            Title = "Profile Version 1",
            Content = "Comprehensive disease profile details including epidemiology, risk factors, and mitigation strategies.",
            IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        return Task.FromResult<ProfileVersionDto?>(version);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProfileSearchResultDto>> GetProfileSearchResultsAsync(
        string? searchText = null,
        bool displayPublished = true,
        bool displayDraft = false,
        bool displayScenarios = false,
        CancellationToken cancellationToken = default)
    {
        var allProfiles = GetAllSearchProfiles();

        var filtered = allProfiles
            .Where(p =>
            {
                // Filter by status
                var matchesStatus = (displayPublished && p.Status == "Published") ||
                                   (displayDraft && p.Status == "Draft") ||
                                   (displayScenarios && p.Status == "Scenario");
                if (!matchesStatus) return false;

                // Filter by search text if provided
                if (!string.IsNullOrEmpty(searchText))
                {
                    return p.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
                }

                return true;
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ProfileSearchResultDto>>(filtered);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProfileSearchResultDto>> GetProfilesByLetterAsync(
        string letter,
        CancellationToken cancellationToken = default)
    {
        var allProfiles = GetAllSearchProfiles();

        var filtered = allProfiles
            .Where(p => letter == "All" || p.Title.StartsWith(letter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<ProfileSearchResultDto>>(filtered);
    }

    /// <summary>Gets all enriched profile data for search operations.</summary>
    private static List<ProfileSearchResultDto> GetAllSearchProfiles()
    {
        return new List<ProfileSearchResultDto>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Bovine tuberculosis",
                Status = "Published",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-6),
                ModifiedAtUtc = DateTime.UtcNow.AddDays(-15),
                IsPublic = true,
                AffectedSpecies = new[] { "Cattle", "Badger", "Deer" },
                PublishedVersions = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                        VersionNumber = 2,
                        Title = "Bovine tuberculosis - v2",
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-15),
                        IsScenario = false
                    },
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("11111111-0000-0000-0000-000000000002"),
                        VersionNumber = 1,
                        Title = "Bovine tuberculosis - v1",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-6),
                        IsScenario = false
                    }
                },
                DraftVersions = new List<ProfileHistoryItemDto>(),
                Scenarios = new List<ProfileHistoryItemDto>()
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "Avian influenza",
                Status = "Draft",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-3),
                ModifiedAtUtc = DateTime.UtcNow.AddDays(-2),
                IsPublic = false,
                AffectedSpecies = new[] { "Poultry", "Wild birds" },
                PublishedVersions = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("22222222-0000-0000-0000-000000000001"),
                        VersionNumber = 1,
                        Title = "Avian influenza - v1",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-12),
                        IsScenario = false
                    }
                },
                DraftVersions = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("22222222-0000-0000-0000-000000000002"),
                        VersionNumber = 2,
                        Title = "Avian influenza - v2 (Draft)",
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                        IsScenario = false
                    }
                },
                Scenarios = new List<ProfileHistoryItemDto>()
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Title = "Brucellosis",
                Status = "Published",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-8),
                ModifiedAtUtc = DateTime.UtcNow.AddDays(-45),
                IsPublic = true,
                AffectedSpecies = new[] { "Cattle", "Sheep", "Goats" },
                PublishedVersions = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("33333333-0000-0000-0000-000000000001"),
                        VersionNumber = 1,
                        Title = "Brucellosis - v1",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-8),
                        IsScenario = false
                    }
                },
                DraftVersions = new List<ProfileHistoryItemDto>(),
                Scenarios = new List<ProfileHistoryItemDto>()
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Title = "Bluetongue",
                Status = "Draft",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-2),
                ModifiedAtUtc = DateTime.UtcNow.AddDays(-5),
                IsPublic = false,
                AffectedSpecies = new[] { "Sheep", "Cattle", "Deer" },
                PublishedVersions = new List<ProfileHistoryItemDto>(),
                DraftVersions = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("44444444-0000-0000-0000-000000000001"),
                        VersionNumber = 1,
                        Title = "Bluetongue - v1 (Draft)",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-2),
                        IsScenario = false
                    }
                },
                Scenarios = new List<ProfileHistoryItemDto>()
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Title = "Foot and mouth disease",
                Status = "Published",
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-12),
                ModifiedAtUtc = DateTime.UtcNow.AddDays(-30),
                IsPublic = true,
                AffectedSpecies = new[] { "Cattle", "Sheep", "Pigs", "Goats" },
                PublishedVersions = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("55555555-0000-0000-0000-000000000001"),
                        VersionNumber = 3,
                        Title = "Foot and mouth disease - v3",
                        CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
                        IsScenario = false
                    },
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("55555555-0000-0000-0000-000000000002"),
                        VersionNumber = 2,
                        Title = "Foot and mouth disease - v2",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-4),
                        IsScenario = false
                    },
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("55555555-0000-0000-0000-000000000003"),
                        VersionNumber = 1,
                        Title = "Foot and mouth disease - v1",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-12),
                        IsScenario = false
                    }
                },
                DraftVersions = new List<ProfileHistoryItemDto>(),
                Scenarios = new[]
                {
                    new ProfileHistoryItemDto
                    {
                        VersionId = Guid.Parse("55555555-0000-0000-0000-000000000004"),
                        VersionNumber = 1,
                        Title = "Foot and mouth disease - Scenario 1",
                        CreatedAtUtc = DateTime.UtcNow.AddMonths(-2),
                        IsScenario = true
                    }
                }
            }
        };
    }
}
