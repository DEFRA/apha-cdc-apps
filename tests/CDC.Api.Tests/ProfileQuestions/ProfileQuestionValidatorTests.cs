using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Queries;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileQuestions;

public class ProfileQuestionValidatorTests
{
    [Fact]
    public void GetProfileQuestionQueryValidator_EmptyQuestionId_ShouldFail()
    {
        var result = new GetProfileQuestionQueryValidator().Validate(new GetProfileQuestionQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileQuestionQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileQuestionQueryValidator().Validate(new GetProfileQuestionQuery(ProfileQuestionTestData.QuestionId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileQuestionInfoListQueryValidator_EmptyProfileSectionId_ShouldFail()
    {
        var result = new GetProfileQuestionInfoListQueryValidator().Validate(new GetProfileQuestionInfoListQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileGuidanceReportQueryValidator_InvalidReportType_ShouldFail()
    {
        var result = new GetProfileGuidanceReportQueryValidator().Validate(new GetProfileGuidanceReportQuery((ProfileGuidanceReportType)999));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileGuidanceReportQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileGuidanceReportQueryValidator().Validate(new GetProfileGuidanceReportQuery(ProfileGuidanceReportType.All));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateProfileQuestionCommandValidator_EmptyQuestionId_ShouldFail()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = Guid.Empty,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        var result = new UpdateProfileQuestionCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateProfileQuestionCommandValidator_EmptyName_ShouldFail()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = string.Empty,
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        var result = new UpdateProfileQuestionCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateProfileQuestionCommandValidator_EmptyUserGuidance_ShouldFail()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = string.Empty,
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        var result = new UpdateProfileQuestionCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateProfileQuestionCommandValidator_InvalidRowVersionLength_ShouldFail()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = [1, 2, 3]
        };

        var result = new UpdateProfileQuestionCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateProfileQuestionCommandValidator_ValidRequest_ShouldPass()
    {
        var command = new UpdateProfileQuestionCommand
        {
            Id = ProfileQuestionTestData.QuestionId,
            Name = "Name",
            NonTechnicalName = "Non-technical name",
            UserGuidance = "Guidance",
            LastUpdated = ProfileQuestionTestData.RowVersion
        };

        var result = new UpdateProfileQuestionCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
