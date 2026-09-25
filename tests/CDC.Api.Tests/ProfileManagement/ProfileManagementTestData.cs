using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement.Dtos;

namespace CDC.Api.Tests.ProfileManagement;

/// <summary>
/// Builders for profile management domain objects and DTOs. The entities use
/// <c>required</c> members, which AutoFixture cannot populate, so they are constructed
/// explicitly here.
/// </summary>
internal static class ProfileManagementTestData
{
    public static readonly Guid ProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProfileVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid NewProfileVersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid SpeciesId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ProfileStatusId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static byte[] RowVersion => [0, 0, 0, 0, 0, 0, 7, 209];

    public static byte[] NewRowVersion => [0, 0, 0, 0, 0, 0, 7, 210];

    public static AffectedSpeciesInfo AffectedSpecies(bool isActive = true, string type = "Profiled") => new()
    {
        SpeciesId = SpeciesId,
        Name = "Cattle",
        Type = type,
        IsActive = isActive
    };

    public static Profile Profile() => new()
    {
        Id = ProfileId,
        Title = "Bovine tuberculosis",
        ScenarioTitle = string.Empty,
        ParentId = Guid.Empty,
        ParentTitle = string.Empty,
        CurrentDraftProfileVersionId = ProfileVersionId,
        CurrentPublishedProfileVersionId = Guid.Empty,
        CurrentPublicVersionId = Guid.Empty,
        HasPublicScenarios = false,
        ProfileStatusId = ProfileStatusId,
        LastUpdated = RowVersion,
        AffectedSpecies = [AffectedSpecies()]
    };

    public static ProfileAttributesDto ProfileAttributesDto() => new()
    {
        Id = ProfileId,
        Title = "Bovine tuberculosis",
        LastUpdated = RowVersion
    };
}
