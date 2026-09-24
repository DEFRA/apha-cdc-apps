using CDC.Api.Features.Species.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Species;

public class UpdateSpeciesNameParentCommandValidatorTests
{
    private readonly UpdateSpeciesNameParentCommandValidator validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = validator.Validate(SpeciesTestData.UpdateNameParentCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingSpeciesId_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { SpeciesId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.SpeciesId));
    }

    [Fact]
    public void MissingUserId_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { UserId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.UserId));
    }

    [Fact]
    public void MissingName_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { Name = string.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You need to provide a new name for this species.");
    }

    [Fact]
    public void NameTooLong_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { Name = new string('a', 51) };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The name must be no longer than 50 characters.");
    }

    [Fact]
    public void MissingReason_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { Reason = string.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You need to provide a reason for this change.");
    }

    [Fact]
    public void ReasonTooLong_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { Reason = new string('a', 256) };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The reason for change must be no longer than 255 characters.");
    }

    [Fact]
    public void MissingRowVersion_ShouldFail()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { LastUpdated = [1, 2] };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The row version must be 8 bytes.");
    }

    [Fact]
    public void RootParent_ShouldPass()
    {
        var command = SpeciesTestData.UpdateNameParentCommand() with { ParentId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
