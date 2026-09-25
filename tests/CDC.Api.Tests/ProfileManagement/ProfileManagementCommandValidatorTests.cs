using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileManagement;

public class ProfileManagementCommandValidatorTests
{
    [Fact]
    public void CreateProfileCommandValidator_RequiresId()
    {
        var command = new CreateProfileCommand { Id = Guid.Empty, Title = "Anthrax" };

        var result = new CreateProfileCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(CreateProfileCommand.Id));
    }

    [Fact]
    public void CreateProfileCommandValidator_RequiresTitle_ForCurrentSituationProfile()
    {
        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            Title = string.Empty
        };

        var result = new CreateProfileCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(CreateProfileCommand.Title));
    }

    [Fact]
    public void CreateProfileCommandValidator_RequiresParentTitle_ForScenario()
    {
        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            ParentId = ProfileManagementTestData.ProfileId,
            ParentTitle = string.Empty
        };

        var result = new CreateProfileCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(CreateProfileCommand.ParentTitle));
    }

    [Fact]
    public void CreateProfileCommandValidator_Passes_ForValidCommand()
    {
        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            Title = "Anthrax",
            AffectedSpeciesInsertList =
            [
                new AffectedSpeciesInsertDto
                {
                    ProfileVersionId = ProfileManagementTestData.ProfileVersionId,
                    SpeciesId = ProfileManagementTestData.SpeciesId,
                    Type = "Profiled"
                }
            ]
        };

        var result = new CreateProfileCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateProfileCommandValidator_Rejects_UnknownAffectedSpeciesType()
    {
        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            Title = "Anthrax",
            AffectedSpeciesInsertList =
            [
                new AffectedSpeciesInsertDto
                {
                    ProfileVersionId = ProfileManagementTestData.ProfileVersionId,
                    SpeciesId = ProfileManagementTestData.SpeciesId,
                    Type = "Unknown"
                }
            ]
        };

        var result = new CreateProfileCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateProfileAttributesCommandValidator_RequiresEightByteRowVersion()
    {
        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            LastUpdated = [1, 2, 3]
        };

        var result = new UpdateProfileAttributesCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(UpdateProfileAttributesCommand.LastUpdated));
    }

    [Fact]
    public void UpdateProfileAttributesCommandValidator_Passes_ForValidCommand()
    {
        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            LastUpdated = ProfileManagementTestData.RowVersion
        };

        var result = new UpdateProfileAttributesCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteProfileVersionCommandValidator_RequiresProfileVersionId()
    {
        var result = new DeleteProfileVersionCommandValidator().Validate(new DeleteProfileVersionCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateNewProfileVersionCommandValidator_RequiresProfileVersionId()
    {
        var result = new CreateNewProfileVersionCommandValidator()
            .Validate(new CreateNewProfileVersionCommand(Guid.Empty, IsPublished: true, IsPublic: false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateNewProfileVersionCommandValidator_Rejects_PublicDraft()
    {
        var command = new CreateNewProfileVersionCommand(
            ProfileManagementTestData.ProfileVersionId,
            IsPublished: false,
            IsPublic: true);

        var result = new CreateNewProfileVersionCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(CreateNewProfileVersionCommand.IsPublic));
    }

    [Fact]
    public void CreateNewProfileVersionCommandValidator_Passes_ForPublishedPublicVersion()
    {
        var command = new CreateNewProfileVersionCommand(
            ProfileManagementTestData.ProfileVersionId,
            IsPublished: true,
            IsPublic: true);

        var result = new CreateNewProfileVersionCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateProfileStatusCommandValidator_RequiresBothIds()
    {
        var result = new UpdateProfileStatusCommandValidator().Validate(new UpdateProfileStatusCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void SetProfileVersionPublicAccessCommandValidator_RequiresProfileVersionId()
    {
        var result = new SetProfileVersionPublicAccessCommandValidator().Validate(new SetProfileVersionPublicAccessCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
