using CDC.Api.Features.ProfileSections.Queries;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileSections;

public class ProfileSectionValidatorTests
{
    [Fact]
    public void GetProfileVersionValidationStatusQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetProfileVersionValidationStatusQueryValidator().Validate(new GetProfileVersionValidationStatusQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionValidationStatusQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileVersionValidationStatusQueryValidator().Validate(
            new GetProfileVersionValidationStatusQuery(ProfileSectionTestData.ProfileVersionId));

        result.IsValid.Should().BeTrue();
    }
}
