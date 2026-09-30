using CDC.Api.Features.ProfileManagement.Queries;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileManagement;

public class ProfileManagementQueryValidatorTests
{
    [Fact]
    public void GetProfileAttributesQueryValidator_RequiresProfileId()
    {
        var result = new GetProfileAttributesQueryValidator().Validate(new GetProfileAttributesQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileAttributesQueryValidator_Passes_ForValidQuery()
    {
        var result = new GetProfileAttributesQueryValidator().Validate(new GetProfileAttributesQuery(ProfileManagementTestData.ProfileId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetAffectedSpeciesQueryValidator_RequiresSpeciesId()
    {
        var result = new GetAffectedSpeciesQueryValidator().Validate(new GetAffectedSpeciesQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetAffectedSpeciesQueryValidator_Passes_ForValidQuery()
    {
        var result = new GetAffectedSpeciesQueryValidator().Validate(new GetAffectedSpeciesQuery(ProfileManagementTestData.SpeciesId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetNewProfileDefaultsQueryValidator_RequiresCloneProfileVersionId()
    {
        var result = new GetNewProfileDefaultsQueryValidator().Validate(new GetNewProfileDefaultsQuery(Guid.Empty, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetNewProfileDefaultsQueryValidator_Passes_ForValidQuery()
    {
        var result = new GetNewProfileDefaultsQueryValidator()
            .Validate(new GetNewProfileDefaultsQuery(ProfileManagementTestData.ProfileVersionId, true));

        result.IsValid.Should().BeTrue();
    }
}
