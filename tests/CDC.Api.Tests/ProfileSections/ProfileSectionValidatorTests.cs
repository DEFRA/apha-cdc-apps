using CDC.Api.Features.ProfileSections.Commands;
using CDC.Api.Features.ProfileSections.Queries;
using CDC.Common.Contracts;
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

    [Fact]
    public void ValidateProfileVersionSectionQueryValidator_EmptyIds_ShouldFail()
    {
        var result = new ValidateProfileVersionSectionQueryValidator().Validate(new ValidateProfileVersionSectionQuery(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateProfileVersionSectionQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new ValidateProfileVersionSectionQueryValidator().Validate(
            new ValidateProfileVersionSectionQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ClearProfileVersionSectionCommandValidator_EmptyIds_ShouldFail()
    {
        var result = new ClearProfileVersionSectionCommandValidator().Validate(
            new ClearProfileVersionSectionCommand { ProfileVersionId = Guid.Empty, ProfileSectionId = Guid.Empty });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ClearProfileVersionSectionCommandValidator_ValidRequest_ShouldPass()
    {
        var result = new ClearProfileVersionSectionCommandValidator().Validate(
            new ClearProfileVersionSectionCommand
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId
            });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SaveProfileVersionSectionCommandValidator_EmptyIds_ShouldFail()
    {
        var result = new SaveProfileVersionSectionCommandValidator().Validate(
            new SaveProfileVersionSectionCommand { ProfileVersionId = Guid.Empty, ProfileSectionId = Guid.Empty, Answers = [] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveProfileVersionSectionCommandValidator_ValidRequest_ShouldPass()
    {
        var result = new SaveProfileVersionSectionCommandValidator().Validate(
            new SaveProfileVersionSectionCommand
            {
                ProfileVersionId = ProfileSectionTestData.ProfileVersionId,
                ProfileSectionId = ProfileSectionTestData.SectionId,
                Answers = []
            });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileVersionSectionContributionsQueryValidator_EmptyIds_ShouldFail()
    {
        var result = new GetProfileVersionSectionContributionsQueryValidator().Validate(
            new GetProfileVersionSectionContributionsQuery(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionSectionContributionsQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileVersionSectionContributionsQueryValidator().Validate(
            new GetProfileVersionSectionContributionsQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileSectionReviewCommentsQueryValidator_EmptyIds_ShouldFail()
    {
        var result = new GetProfileSectionReviewCommentsQueryValidator().Validate(
            new GetProfileSectionReviewCommentsQuery(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileSectionReviewCommentsQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileSectionReviewCommentsQueryValidator().Validate(
            new GetProfileSectionReviewCommentsQuery(ProfileSectionTestData.ProfileVersionId, ProfileSectionTestData.SectionId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileVersionAllRevisionDatesQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetProfileVersionAllRevisionDatesQueryValidator().Validate(new GetProfileVersionAllRevisionDatesQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionAllRevisionDatesQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileVersionAllRevisionDatesQueryValidator().Validate(
            new GetProfileVersionAllRevisionDatesQuery(ProfileSectionTestData.ProfileVersionId));

        result.IsValid.Should().BeTrue();
    }
}
