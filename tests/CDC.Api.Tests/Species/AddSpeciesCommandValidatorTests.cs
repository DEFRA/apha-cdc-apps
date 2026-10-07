using CDC.Api.Features.Species.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Species;

public class AddSpeciesCommandValidatorTests
{
    private readonly AddSpeciesCommandValidator validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = validator.Validate(SpeciesTestData.AddSpeciesCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RootParent_ShouldPass()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { ParentId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingName_ShouldFail()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { Name = string.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You need to provide a new name for this species");
    }

    [Fact]
    public void NameTooLong_ShouldFail()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { Name = new string('a', 51) };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The new species name must be no longer than 50 characters");
    }

    [Fact]
    public void MissingParent_ShouldFail()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { ParentId = null };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You must select a new parent for the species");
    }

    [Fact]
    public void MissingReason_ShouldFail()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { Reason = string.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You need to provide a reason for this change");
    }

    [Fact]
    public void ReasonTooLong_ShouldFail()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { Reason = new string('a', 256) };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The reason for change must be no longer than 255 characters");
    }

    [Fact]
    public void MissingUserId_ShouldFail()
    {
        var command = SpeciesTestData.AddSpeciesCommand() with { UserId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.UserId));
    }
}
